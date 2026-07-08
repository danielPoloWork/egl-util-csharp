using System.Collections.Concurrent;

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

    // Records the Sequence of every delivered event (in delivery order) so a test can assert exactly-once
    // by payload — not just by count — and the single-consumer FIFO order.
    private sealed class RecordingHandler : IEventHandler<Pinged>
    {
        private readonly ConcurrentQueue<int> _sequences = new();

        // Snapshot in delivery order; safe to read once the broker has drained (StopAsync).
        public IReadOnlyList<int> Sequences => _sequences.ToArray();

        public Task HandleAsync(Pinged @event, CancellationToken cancellationToken = default)
        {
            _sequences.Enqueue(@event.Sequence);
            return Task.CompletedTask;
        }
    }

    // Records deliveries, but blocks the very first one on a gate so subsequent events pile up behind the
    // single consumer — a deterministic queued backlog for the shutdown-drain test.
    private sealed class GatedRecordingHandler : IEventHandler<Pinged>
    {
        private readonly Task _gate;
        private readonly ConcurrentQueue<int> _sequences = new();
        private int _seen;

        public GatedRecordingHandler(Task gate) => _gate = gate;

        public IReadOnlyList<int> Sequences => _sequences.ToArray();

        public async Task HandleAsync(Pinged @event, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _seen) == 1)
            {
                await _gate; // hold the consumer on the first event while the rest queue up
            }

            _sequences.Enqueue(@event.Sequence);
        }
    }

    private static ServiceProvider BuildProvider(IEventHandler<Pinged> handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton(handler);
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
    public async Task PublishAsync_after_shutdown_throws()
    {
        // #55: once the broker has stopped it no longer accepts events — the closed channel surfaces as
        // an InvalidOperationException rather than silently swallowing the publish.
        var handler = new CountingHandler();
        await using ServiceProvider provider = BuildProvider(handler);
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>());
        await broker.StartAsync(CancellationToken.None);
        await broker.StopAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => broker.PublishAsync(new Pinged(1)).AsTask());
        Assert.Equal(0, handler.Count); // the rejected event was never delivered
    }

    [Fact]
    public async Task StopAsync_drains_a_queued_backlog()
    {
        // #55: shutdown is a best-effort drain. A gate holds the consumer on the first event so the rest
        // form a real queued backlog; shutdown then begins, the gate is released, and StopAsync must drain
        // the whole backlog before returning (standalone broker, no shutdown-timeout budget). Under a
        // generic host the drain is bounded by HostOptions.ShutdownTimeout, so deep queues can lose the
        // tail — a host-integration behavior not exercised here.
        const int count = 50;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new GatedRecordingHandler(gate.Task);
        await using ServiceProvider provider = BuildProvider(handler);
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>());
        await broker.StartAsync(CancellationToken.None);

        for (int i = 0; i < count; i++)
        {
            await broker.PublishAsync(new Pinged(i)); // event 0 is gated; 1..49 queue behind it
        }

        Task stop = broker.StopAsync(CancellationToken.None); // begins shutdown while the backlog is queued
        gate.SetResult(); // release the consumer; the whole backlog must still drain
        await stop.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(count, handler.Sequences.Count); // every queued event drained on shutdown
    }

    [Fact]
    public async Task Delivers_every_event_exactly_once_under_concurrent_publishers()
    {
        const int publishers = 8;
        const int perPublisher = 500;
        const int total = publishers * perPublisher;
        var handler = new RecordingHandler();
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

        // Exactly once by payload, not just by count: sorted, the delivered sequence numbers are exactly
        // 0..total-1. A single sorted-sequence comparison proves both no-loss and no-duplicate — a defect
        // that duplicates one event and drops another (net count unchanged) shifts the sorted sequence and
        // is caught, unlike the old count-only assertion.
        Assert.Equal(Enumerable.Range(0, total), handler.Sequences.OrderBy(sequence => sequence));
    }

    [Fact]
    public async Task Delivers_events_from_a_single_publisher_in_publish_order()
    {
        const int count = 500;
        var handler = new RecordingHandler();
        await using ServiceProvider provider = BuildProvider(handler);
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>());
        await broker.StartAsync(CancellationToken.None);

        for (int i = 0; i < count; i++)
        {
            await broker.PublishAsync(new Pinged(i));
        }

        await broker.StopAsync(CancellationToken.None);

        // The single consumer processes events sequentially in publish order (the documented FIFO
        // contract): one publisher's events are delivered exactly in the order they were published.
        Assert.Equal(Enumerable.Range(0, count), handler.Sequences);
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

    // A scoped handler that implements IAsyncDisposable but NOT IDisposable: synchronous scope
    // disposal throws for such a service, so the dispatch scope must be disposed asynchronously.
    private sealed class AsyncDisposableHandler : IEventHandler<Pinged>, IAsyncDisposable
    {
        private readonly TaskCompletionSource _handled;
        private readonly TaskCompletionSource _disposed;

        public AsyncDisposableHandler(TaskCompletionSource handled, TaskCompletionSource disposed)
        {
            _handled = handled;
            _disposed = disposed;
        }

        public Task HandleAsync(Pinged @event, CancellationToken cancellationToken = default)
        {
            _handled.TrySetResult();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _disposed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Disposes_an_async_disposable_scoped_service_without_failing_dispatch()
    {
        // Regression for #60: the per-dispatch scope owns an IAsyncDisposable-only handler. A synchronous
        // scope dispose throws (leaking it and falsely logging dispatch as failed); async disposal works.
        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        services.AddScoped<IEventHandler<Pinged>>(_ => new AsyncDisposableHandler(handled, disposed));
        await using ServiceProvider provider = services.BuildServiceProvider();
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>());
        await broker.StartAsync(CancellationToken.None);

        await broker.PublishAsync(new Pinged(1));
        await handled.Task.WaitAsync(TimeSpan.FromSeconds(30));   // dispatch ran
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(30));  // and the scope disposed it via DisposeAsync

        await broker.StopAsync(CancellationToken.None);
    }

    // --- #99: per-event DI scope, runtime-type dispatch, no-handler survival, end-to-end wiring ---

    private sealed class ScopeTracker
    {
        public ConcurrentBag<ScopedDependency> Created { get; } = [];

        public ConcurrentBag<ScopedDependency> Disposed { get; } = [];
    }

    private sealed class ScopedDependency : IDisposable
    {
        private readonly ScopeTracker _tracker;

        public ScopedDependency(ScopeTracker tracker)
        {
            _tracker = tracker;
            tracker.Created.Add(this);
        }

        public void Dispose() => _tracker.Disposed.Add(this);
    }

    // A scoped handler that depends on a scoped IDisposable, so resolving it in the per-event scope both
    // creates the dependency and lets scope disposal dispose it.
    private sealed class ScopedHandler : IEventHandler<Pinged>
    {
        public ScopedHandler(ScopedDependency dependency) => _ = dependency;

        public Task HandleAsync(Pinged @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed record Unhandled(int Value);

    private sealed record E2E(int Value);

    private sealed class E2ESignal
    {
        public TaskCompletionSource<int> Handled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    // A transient handler the assembly scan will register, resolving a singleton signal so DI can build it.
    private sealed class E2EHandler : IEventHandler<E2E>
    {
        private readonly E2ESignal _signal;

        public E2EHandler(E2ESignal signal) => _signal = signal;

        public Task HandleAsync(E2E @event, CancellationToken cancellationToken = default)
        {
            _signal.Handled.TrySetResult(@event.Value);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Resolves_a_fresh_scope_per_event_and_disposes_it()
    {
        // #99: each event is dispatched in its own DI scope, so a scoped dependency is created fresh per
        // event and disposed when that event's scope closes — not reused from the root provider.
        var tracker = new ScopeTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddScoped<ScopedDependency>();
        services.AddScoped<IEventHandler<Pinged>, ScopedHandler>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>());
        await broker.StartAsync(CancellationToken.None);

        await broker.PublishAsync(new Pinged(1));
        await broker.PublishAsync(new Pinged(2));
        await broker.StopAsync(CancellationToken.None); // drains both; each per-event scope is disposed

        Assert.Equal(2, tracker.Created.Count); // a fresh dependency per event (each ctor adds a new instance), not one shared
        Assert.Equal(2, tracker.Disposed.Count); // each per-event scope disposed its dependency
    }

    [Fact]
    public async Task Dispatches_by_the_events_runtime_type_not_its_static_type()
    {
        // #99: publishing through a base static type must resolve handlers by the RUNTIME type. The static
        // type here is object, but the runtime type is Pinged, so the Pinged handler runs.
        var handler = new CountingHandler();
        await using ServiceProvider provider = BuildProvider(handler);
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>());
        await broker.StartAsync(CancellationToken.None);

        object baseTyped = new Pinged(1);
        await broker.PublishAsync(baseTyped); // PublishAsync<object>(Pinged) — dispatched by runtime type
        await broker.StopAsync(CancellationToken.None);

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task An_event_with_no_registered_handler_does_not_stop_later_delivery()
    {
        // #99: an event type with no registered handler is a no-op (empty handler set); the consumer
        // survives and a later event that does have a handler still delivers.
        var handler = new CountingHandler(); // registered for Pinged only
        await using ServiceProvider provider = BuildProvider(handler);
        using var broker = new EventBroker(provider.GetRequiredService<IServiceScopeFactory>());
        await broker.StartAsync(CancellationToken.None);

        await broker.PublishAsync(new Unhandled(0)); // no handler for Unhandled
        await broker.PublishAsync(new Pinged(1)); // has a handler
        await broker.StopAsync(CancellationToken.None);

        Assert.Equal(1, handler.Count); // the unhandled event neither threw nor stopped the consumer
    }

    [Fact]
    public async Task End_to_end_delivers_to_a_scanned_transient_handler_wired_by_AddD4npUtil()
    {
        // #99: exercises the shipped path end-to-end — AddD4npUtil scans and registers handlers as
        // TRANSIENT and wires the broker — rather than the pre-built singletons the other tests use.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<E2ESignal>();
        services.AddD4npUtil(typeof(EventBrokerTests).Assembly); // scans E2EHandler (transient) + wires the broker
        await using ServiceProvider provider = services.BuildServiceProvider();
        var signal = provider.GetRequiredService<E2ESignal>();
        var broker = provider.GetRequiredService<EventBroker>();
        await broker.StartAsync(CancellationToken.None);

        await ((IEventBroker)broker).PublishAsync(new E2E(42));
        int value = await signal.Handled.Task.WaitAsync(TimeSpan.FromSeconds(30));

        await broker.StopAsync(CancellationToken.None);
        Assert.Equal(42, value); // the scanned transient handler received the event
    }
}
