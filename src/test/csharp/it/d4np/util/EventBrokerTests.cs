using It.D4np.Util.Application;
using It.D4np.Util.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

namespace It.D4np.Util.Tests;

public class EventBrokerTests
{
    private sealed record Pinged(int Sequence);

    private sealed class CountingHandler : IEventHandler<Pinged>
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public Task HandleAsync(Pinged @event, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IEventHandler<Pinged>
    {
        public Task HandleAsync(Pinged @event, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("handler failure");
    }

    private static ServiceProvider BuildProvider(CountingHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<Pinged>>(handler);
        return services.BuildServiceProvider();
    }

    // A handler that re-publishes a follow-up event (event chaining) on the first event.
    private sealed class ChainingHandler : IEventHandler<Pinged>
    {
        private readonly IEventBroker _broker;
        private readonly TaskCompletionSource _followUpHandled;
        private int _count;

        public ChainingHandler(IEventBroker broker, TaskCompletionSource followUpHandled)
        {
            _broker = broker;
            _followUpHandled = followUpHandled;
        }

        public int Count => Volatile.Read(ref _count);

        public async Task HandleAsync(Pinged @event, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            if (@event.Sequence == 1)
            {
                await _broker.PublishAsync(new Pinged(2), cancellationToken); // re-entrant follow-up
            }
            else
            {
                _followUpHandled.TrySetResult();
            }
        }
    }

    // A handler that, while the single consumer is parked in it, re-publishes twice into a capacity-1
    // channel: the first fills the slot, the second hits a full channel.
    private sealed class FullChannelRepublishingHandler : IEventHandler<Pinged>
    {
        private readonly IEventBroker _broker;
        private readonly TaskCompletionSource _done;

        public FullChannelRepublishingHandler(IEventBroker broker, TaskCompletionSource done)
        {
            _broker = broker;
            _done = done;
        }

        public bool CaughtFull { get; private set; }

        public async Task HandleAsync(Pinged @event, CancellationToken cancellationToken = default)
        {
            if (@event.Sequence != 1)
            {
                return; // only the first event re-publishes
            }

            await _broker.PublishAsync(new Pinged(2), cancellationToken); // enqueues (the one slot was free)
            try
            {
                await _broker.PublishAsync(new Pinged(3), cancellationToken); // channel full — must fail fast, not block
            }
            catch (InvalidOperationException)
            {
                CaughtFull = true;
            }
            finally
            {
                _done.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task A_handler_may_publish_a_follow_up_event_that_is_delivered()
    {
        var followUpHandled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        services.AddSingleton(sp => new EventBroker(sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<IEventBroker>(sp => sp.GetRequiredService<EventBroker>());
        services.AddSingleton<IEventHandler<Pinged>>(
            sp => new ChainingHandler(sp.GetRequiredService<IEventBroker>(), followUpHandled));
        await using ServiceProvider provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<EventBroker>();
        var handler = (ChainingHandler)provider.GetRequiredService<IEventHandler<Pinged>>();
        await broker.StartAsync(CancellationToken.None);

        await broker.PublishAsync(new Pinged(1));
        await followUpHandled.Task.WaitAsync(TimeSpan.FromSeconds(30)); // the re-published event was delivered

        await broker.StopAsync(CancellationToken.None);
        Assert.Equal(2, handler.Count); // original + follow-up both handled
    }

    [Fact]
    public async Task An_in_handler_publish_on_a_full_channel_fails_fast_instead_of_deadlocking()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        services.AddSingleton(sp => new EventBroker(sp.GetRequiredService<IServiceScopeFactory>(), capacity: 1));
        services.AddSingleton<IEventBroker>(sp => sp.GetRequiredService<EventBroker>());
        services.AddSingleton<IEventHandler<Pinged>>(
            sp => new FullChannelRepublishingHandler(sp.GetRequiredService<IEventBroker>(), done));
        await using ServiceProvider provider = services.BuildServiceProvider();
        var broker = provider.GetRequiredService<EventBroker>();
        var handler = (FullChannelRepublishingHandler)provider.GetRequiredService<IEventHandler<Pinged>>();
        await broker.StartAsync(CancellationToken.None);

        await broker.PublishAsync(new Pinged(1));
        // Before the fix the second in-handler publish blocks forever (self-deadlock); the timeout bounds it.
        await done.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await broker.StopAsync(CancellationToken.None);
        Assert.True(handler.CaughtFull); // the publish onto the full channel failed fast, not deadlocked
    }

    [Fact]
    public async Task Publishes_an_event_to_its_registered_handler()
    {
        var handler = new CountingHandler();
        await using ServiceProvider provider = BuildProvider(handler);
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>());
        await broker.StartAsync(CancellationToken.None);

        await broker.PublishAsync(new Pinged(1));
        await broker.StopAsync(CancellationToken.None); // completes the channel and drains

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Delivers_every_event_exactly_once_under_concurrent_publishers()
    {
        const int publishers = 8;
        const int perPublisher = 500;
        var handler = new CountingHandler();
        await using ServiceProvider provider = BuildProvider(handler);

        // Small capacity so publishers must wait on back-pressure, exercising the bounded channel.
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>(), capacity: 256);
        await broker.StartAsync(CancellationToken.None);

        await Task.WhenAll(Enumerable.Range(0, publishers).Select(publisher => Task.Run(async () =>
        {
            for (int i = 0; i < perPublisher; i++)
            {
                await broker.PublishAsync(new Pinged((publisher * perPublisher) + i));
            }
        })));

        await broker.StopAsync(CancellationToken.None); // drains every queued event before returning

        Assert.Equal(publishers * perPublisher, handler.Count);
    }

    [Fact]
    public async Task PublishAsync_rejects_a_null_event()
    {
        var handler = new CountingHandler();
        await using ServiceProvider provider = BuildProvider(handler);
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => broker.PublishAsync<Pinged>(null!).AsTask());
    }

    [Fact]
    public async Task A_throwing_handler_does_not_stop_delivery_to_siblings_or_later_events()
    {
        var counter = new CountingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<Pinged>>(new ThrowingHandler()); // throws on every event
        services.AddSingleton<IEventHandler<Pinged>>(counter);               // sibling that must still run
        await using ServiceProvider provider = services.BuildServiceProvider();
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>());
        await broker.StartAsync(CancellationToken.None);

        await broker.PublishAsync(new Pinged(1));
        await broker.PublishAsync(new Pinged(2));
        await broker.StopAsync(CancellationToken.None);

        // The failing handler neither stopped its sibling nor killed the consumer before event 2.
        Assert.Equal(2, counter.Count);
    }
}
