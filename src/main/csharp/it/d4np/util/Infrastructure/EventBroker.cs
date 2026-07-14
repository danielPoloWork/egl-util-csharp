using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Channels;

using It.D4np.Util.Application;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// An in-memory <see cref="IEventBroker"/> backed by a bounded <c>System.Threading.Channels</c> channel
/// and a single background consumer. Each published event is delivered, from a fresh DI scope, to every
/// registered <see cref="IEventHandler{TEvent}"/> whose <c>TEvent</c> the event's <b>runtime</b> type is
/// assignable to — its exact type and, honoring the interface's contravariance, any base type or
/// interface (so an <c>IEventHandler&lt;IDomainEvent&gt;</c> receives every event); most-specific handlers
/// run first (#71).
/// </summary>
/// <remarks>
/// <para>
/// <b>Threading.</b> <see cref="PublishAsync{TEvent}"/> is safe to call concurrently and does not block on
/// handler execution (it only awaits enqueueing, which applies bounded back-pressure when the channel is
/// full). A single consumer processes events sequentially in publish order, so handlers run one event at a
/// time. Each accepted event is consumed exactly once. A handler that publishes a follow-up event takes a
/// <b>non-blocking</b> path (it runs on the consumer's flow, so blocking would deadlock): the follow-up is
/// enqueued if there is room, otherwise <see cref="PublishAsync{TEvent}"/> throws instead of waiting.
/// </para>
/// <para>
/// <b>Resilience.</b> A handler that throws is isolated: the consumer continues with the next handler and
/// event — one faulty handler never stops delivery. Every isolated failure is reported through an
/// <see cref="System.Diagnostics.Tracing.EventSource"/> named <c>It.D4np.Util.EventBroker</c>
/// unconditionally (so failures are observable via ETW / <c>dotnet-trace</c> / an
/// <see cref="System.Diagnostics.Tracing.EventListener"/> even when the broker has no logger, #43) and,
/// additionally, logged when a logger is supplied. Handlers are invoked with a non-cancelled token and run
/// to completion; the broker does not force-cancel in-flight handlers.
/// </para>
/// <para>
/// <b>Shutdown.</b> The broker stops accepting new events and makes a <i>best-effort</i> drain of the
/// already-queued events. Under a generic host the drain is bounded by <c>HostOptions.ShutdownTimeout</c>,
/// so events not drained within that budget may be lost (raise the timeout for deep queues).
/// </para>
/// </remarks>
public sealed class EventBroker : BackgroundService, IEventBroker
{
    // Per-broker (not process-static): the cache of closed generic dispatch plans is collected with the
    // broker and its provider, rather than pinning a closed generic type + compiled delegate per distinct
    // published event type for the process lifetime. That keeps a plugin / dynamic-assembly host or an
    // unbounded generic event type (Envelope<TPayload>) from slowly leaking type/delegate metadata; the
    // per-broker recompute is one-time per event type and negligible (#44).
    private readonly ConcurrentDictionary<Type, EventDispatch> _dispatchers = new();

    private readonly Channel<object> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventBroker>? _logger;

    // True on the single consumer's dispatch flow (and the handler calls it awaits). A handler that
    // re-publishes runs on this flow, so its PublishAsync must not block — see PublishAsync.
    private readonly AsyncLocal<bool> _dispatching = new();

    /// <summary>Creates a broker that resolves handlers from <paramref name="scopeFactory"/>.</summary>
    /// <param name="scopeFactory">The DI scope factory used to resolve handlers per event.</param>
    /// <param name="capacity">The bounded channel capacity; publishers wait when it is full.</param>
    /// <param name="logger">
    /// An optional logger used to record handler failures. Failures are always also reported through the
    /// <c>It.D4np.Util.EventBroker</c> <see cref="System.Diagnostics.Tracing.EventSource"/>, so leaving this
    /// <see langword="null"/> does not make them invisible (#43).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="scopeFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    [RequiresDynamicCode(
        "The broker dispatches events by runtime type, closing IEventHandler<>/IEnumerable<> over each " +
        "event type via MakeGenericType and a MakeGenericMethod-built invoker — not compatible with Native AOT.")]
    [RequiresUnreferencedCode(
        "The broker reflects over IEventHandler<> handler types to dispatch; those handler types may be trimmed.")]
    public EventBroker(IServiceScopeFactory scopeFactory, int capacity = 1024, ILogger<EventBroker>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = Channel.CreateBounded<object>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The event is delivered to handlers registered for its <b>runtime</b> type. Throws
    /// <see cref="InvalidOperationException"/> if the broker has stopped and no longer accepts events.
    /// </para>
    /// <para>
    /// <b>Re-entrant (in-handler) publish.</b> A handler runs on the single consumer's flow, which is
    /// the only thing that drains the channel. If such a re-entrant publish blocked for a free slot
    /// under back-pressure it would deadlock (the consumer is parked in the handler and cannot drain),
    /// so an in-handler publish is <b>non-blocking</b>: it enqueues the event if there is room and
    /// otherwise throws <see cref="InvalidOperationException"/> rather than waiting. A handler doing
    /// event chaining should tolerate that failure (catch it, or keep chains short and capacity ample).
    /// External (non-handler) publishers are unaffected and still apply blocking back-pressure.
    /// </para>
    /// </remarks>
    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(@event);

        // In-handler re-publish: never block the consumer's own flow (that self-deadlocks). Enqueue if
        // there is room, else fail fast — the caller (handler) sees the failure instead of hanging.
        if (_dispatching.Value)
        {
            if (!_channel.Writer.TryWrite(@event))
            {
                throw new InvalidOperationException(
                    "A handler could not publish because the event broker's channel would not immediately " +
                    "accept the event (it was full, or closed during shutdown). An in-handler publish is " +
                    "non-blocking to avoid deadlocking the single consumer, so it fails fast instead of " +
                    "waiting — shorten event chains under load or raise the broker capacity.");
            }

            return;
        }

        try
        {
            await _channel.Writer.WriteAsync(@event, cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException exception)
        {
            throw new InvalidOperationException(
                "The event broker has stopped and is no longer accepting events.", exception);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stop accepting new events on shutdown, but drain what is already queued (the read uses None).
        await using CancellationTokenRegistration registration =
            stoppingToken.Register(static state => ((ChannelWriter<object>)state!).TryComplete(), _channel.Writer);

        await foreach (object @event in _channel.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
        {
            try
            {
                await DispatchAsync(@event).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Last-resort isolation: nothing a single event does may stop the only consumer. Report
                // through the EventSource unconditionally (observable without a logger, #43) and through
                // the logger when one was supplied.
                EventBrokerEventSource.Log.DispatchFailed(@event.GetType().ToString(), exception.ToString());
                _logger?.LogError(exception, "Failed to dispatch event {EventType}.", @event.GetType());
            }
        }
    }

    private async Task DispatchAsync(object @event)
    {
        EventDispatch dispatch = _dispatchers.GetOrAdd(@event.GetType(), EventDispatch.ForEventType);

        // Dispose the per-event scope asynchronously: a synchronous Dispose throws for a resolved
        // service that implements IAsyncDisposable but not IDisposable (leaking it and falsely logging
        // the already-succeeded dispatch as failed). AsyncServiceScope still disposes plain
        // IDisposable services synchronously, so this is strictly safer.
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        // Mark this flow as "inside dispatch" so a handler's re-entrant PublishAsync takes the
        // non-blocking path. AsyncLocal flows into the awaited handler calls; reset afterwards.
        _dispatching.Value = true;
        try
        {
            // IEventHandler<in TEvent> is contravariant, so dispatch to handlers registered for the event's
            // runtime type AND each of its base types/interfaces, most-specific first. MS.DI applies no
            // generic variance to IEnumerable<T>, so each applicable closed handler type is resolved
            // explicitly (#71).
            foreach (HandlerGroup group in dispatch.HandlerGroups)
            {
                if (scope.ServiceProvider.GetService(group.EnumerableType) is not IEnumerable handlers)
                {
                    continue;
                }

                foreach (object? handler in handlers)
                {
                    if (handler is null)
                    {
                        continue;
                    }

                    try
                    {
                        // Handlers run to completion with a non-cancelled token; the broker never force-cancels them.
                        await group.Invoke(handler, @event, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        // Isolate failures: one handler must not stop the consumer or its sibling handlers.
                        // Report through the EventSource unconditionally (so the failure is observable even
                        // without a logger, #43) and through the logger when one was supplied.
                        EventBrokerEventSource.Log.HandlerFailed(
                            handler.GetType().ToString(), @event.GetType().ToString(), exception.ToString());
                        _logger?.LogError(
                            exception, "Event handler {HandlerType} failed for event {EventType}.", handler.GetType(), @event.GetType());
                    }
                }
            }
        }
        finally
        {
            _dispatching.Value = false;
        }
    }

    // Per-event-type dispatch, computed once and cached. IEventHandler<in TEvent> is contravariant, so an
    // event is delivered to handlers registered for its runtime type AND any base type/interface. MS.DI
    // does not apply variance when resolving IEnumerable<T>, so the applicable closed handler types are
    // enumerated here (most-specific first), each with the IEnumerable<IEventHandler<T>> service type to
    // resolve and a compiled delegate that calls HandleAsync directly (no reflection per dispatch, so
    // handler exceptions surface as themselves rather than TargetInvocationException) (#71).
    private sealed class EventDispatch
    {
        private EventDispatch(IReadOnlyList<HandlerGroup> handlerGroups) => HandlerGroups = handlerGroups;

        public IReadOnlyList<HandlerGroup> HandlerGroups { get; }

        public static EventDispatch ForEventType(Type eventType)
        {
            var groups = new List<HandlerGroup>();
            foreach (Type handledType in ContravariantHandledTypes(eventType))
            {
                Type enumerableType =
                    typeof(IEnumerable<>).MakeGenericType(typeof(IEventHandler<>).MakeGenericType(handledType));
                var invoke = (Func<object, object, CancellationToken, Task>)typeof(EventDispatch)
                    .GetMethod(nameof(BuildInvoker), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(handledType)
                    .Invoke(null, null)!;
                groups.Add(new HandlerGroup(enumerableType, invoke));
            }

            return new EventDispatch(groups);
        }

        // The set of T for which IEventHandler<T> can consume this event by contravariance: the runtime
        // type, then its base classes up to object, then every interface it implements. TEvent : class, so
        // all are reference types. Most-specific first, so exact-type handlers run before base-typed ones.
        private static IEnumerable<Type> ContravariantHandledTypes(Type eventType)
        {
            for (Type? type = eventType; type is not null; type = type.BaseType)
            {
                yield return type;
            }

            foreach (Type contract in eventType.GetInterfaces())
            {
                yield return contract;
            }
        }

        private static Func<object, object, CancellationToken, Task> BuildInvoker<TEvent>()
            where TEvent : class =>
            (handler, @event, cancellationToken) => ((IEventHandler<TEvent>)handler).HandleAsync((TEvent)@event, cancellationToken);
    }

    // One applicable closed handler type for an event: the IEnumerable<IEventHandler<T>> to resolve from
    // the scope, and the delegate that invokes IEventHandler<T>.HandleAsync (casting the event to T).
    private sealed class HandlerGroup(Type enumerableType, Func<object, object, CancellationToken, Task> invoke)
    {
        public Type EnumerableType { get; } = enumerableType;

        public Task Invoke(object handler, object @event, CancellationToken cancellationToken) =>
            invoke(handler, @event, cancellationToken);
    }
}
