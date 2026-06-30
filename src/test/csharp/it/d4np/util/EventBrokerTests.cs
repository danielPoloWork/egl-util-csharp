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
