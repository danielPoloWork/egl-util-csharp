using It.D4np.Util.Application;
using It.D4np.Util.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

namespace It.D4np.Util.Tests;

// Regression for #89 (ADR-0038): a handler whose CONSTRUCTION throws must not drop the event for its healthy
// sibling handlers. MS.DI materializes IEnumerable<IEventHandler<T>> eagerly (verified: one throwing ctor
// aborts the whole resolution), so the broker falls back to constructing each scanned handler individually
// and isolating the faulty one — the healthy siblings still receive the event.
public class EventBrokerConstructionIsolationTests
{
    // A dedicated event type so the two probe handlers below never perturb (or are perturbed by) the other
    // broker tests that scan this same test assembly.
    private sealed record IsolationProbe(int Value);

    // A singleton sink the (transient, freshly-constructed-per-dispatch) healthy handler writes to, so the
    // test can observe delivery across the broker's per-event handler instances.
    private sealed class IsolationSink
    {
        private int _delivered;

        public int Delivered => Volatile.Read(ref _delivered);

        public void Record() => Interlocked.Increment(ref _delivered);
    }

    // The healthy sibling: depends on the sink (resolved from DI) and records each delivery.
    private sealed class HealthyIsolationHandler : IEventHandler<IsolationProbe>
    {
        private readonly IsolationSink _sink;

        public HealthyIsolationHandler(IsolationSink sink) => _sink = sink;

        public Task HandleAsync(IsolationProbe @event, CancellationToken cancellationToken = default)
        {
            _sink.Record();
            return Task.CompletedTask;
        }
    }

    // The faulty sibling: its CONSTRUCTOR always throws (models a bad ctor or a misconfigured dependency).
    // MS.DI constructs it while materializing the handler IEnumerable, so this aborts the eager resolution.
    private sealed class BrokenCtorIsolationHandler : IEventHandler<IsolationProbe>
    {
        public BrokenCtorIsolationHandler() =>
            throw new InvalidOperationException("this handler's constructor always throws");

        public Task HandleAsync(IsolationProbe @event, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task A_handler_that_fails_construction_does_not_drop_the_event_for_its_healthy_siblings()
    {
        // Two handlers for the same event; one throws in its constructor. Before the fix the eager
        // IEnumerable<IEventHandler<IsolationProbe>> resolution threw and the event was dropped for BOTH
        // handlers — including the perfectly healthy one — contradicting the documented isolation guarantee.
        // The broker now isolates the construction failure so the healthy sibling still receives every event.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IsolationSink>();
        // Scans both probe handlers (transient) and wires the broker — the shipped registration path.
        services.AddD4npUtil(typeof(EventBrokerConstructionIsolationTests).Assembly);
        await using ServiceProvider provider = services.BuildServiceProvider();
        var sink = provider.GetRequiredService<IsolationSink>();
        var broker = provider.GetRequiredService<EventBroker>();
        await broker.StartAsync(CancellationToken.None);

        await ((IEventBroker)broker).PublishAsync(new IsolationProbe(1));
        await ((IEventBroker)broker).PublishAsync(new IsolationProbe(2));
        await broker.StopAsync(CancellationToken.None); // drains both events before returning

        // The healthy sibling received BOTH events despite its sibling's constructor throwing each time.
        Assert.Equal(2, sink.Delivered);
    }
}
