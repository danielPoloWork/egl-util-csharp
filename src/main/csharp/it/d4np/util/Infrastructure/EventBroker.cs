using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;

using It.D4np.Util.Application;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// An in-memory <see cref="IEventBroker"/> backed by a bounded <c>System.Threading.Channels</c> channel
/// and a single background consumer. Each published event is delivered to every registered
/// <see cref="IEventHandler{TEvent}"/>, resolved from a fresh DI scope by the event's <b>runtime</b> type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Threading.</b> <see cref="PublishAsync{TEvent}"/> is safe to call concurrently and does not block on
/// handler execution (it only awaits enqueueing, which applies bounded back-pressure when the channel is
/// full). A single consumer processes events sequentially in publish order, so handlers run one event at a
/// time. Each accepted event is consumed exactly once.
/// </para>
/// <para>
/// <b>Resilience.</b> A handler that throws is isolated: the failure is logged (when a logger is supplied)
/// and the consumer continues with the next handler and event — one faulty handler never stops delivery.
/// Handlers are invoked with a non-cancelled token and run to completion; the broker does not force-cancel
/// in-flight handlers.
/// </para>
/// <para>
/// <b>Shutdown.</b> The broker stops accepting new events and makes a <i>best-effort</i> drain of the
/// already-queued events. Under a generic host the drain is bounded by <c>HostOptions.ShutdownTimeout</c>,
/// so events not drained within that budget may be lost (raise the timeout for deep queues).
/// </para>
/// </remarks>
public sealed class EventBroker : BackgroundService, IEventBroker
{
    private static readonly ConcurrentDictionary<Type, EventDispatch> Dispatchers = new();

    private readonly Channel<object> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventBroker>? _logger;

    /// <summary>Creates a broker that resolves handlers from <paramref name="scopeFactory"/>.</summary>
    /// <param name="scopeFactory">The DI scope factory used to resolve handlers per event.</param>
    /// <param name="capacity">The bounded channel capacity; publishers wait when it is full.</param>
    /// <param name="logger">An optional logger used to record handler failures.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scopeFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
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
    /// The event is delivered to handlers registered for its <b>runtime</b> type. Throws
    /// <see cref="InvalidOperationException"/> if the broker has stopped and no longer accepts events.
    /// </remarks>
    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(@event);

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
                // Last-resort isolation: nothing a single event does may stop the only consumer.
                _logger?.LogError(exception, "Failed to dispatch event {EventType}.", @event.GetType());
            }
        }
    }

    private async Task DispatchAsync(object @event)
    {
        EventDispatch dispatch = Dispatchers.GetOrAdd(@event.GetType(), EventDispatch.ForEventType);

        using IServiceScope scope = _scopeFactory.CreateScope();
        if (scope.ServiceProvider.GetService(dispatch.HandlerEnumerableType) is not IEnumerable handlers)
        {
            return;
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
                await dispatch.Invoke(handler, @event, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Isolate failures: one handler must not stop the consumer or its sibling handlers.
                _logger?.LogError(
                    exception, "Event handler {HandlerType} failed for event {EventType}.", handler.GetType(), @event.GetType());
            }
        }
    }

    // Per-event-type dispatch, computed once and cached: the IEnumerable<IEventHandler<TEvent>> service
    // type to resolve, plus a compiled delegate that calls HandleAsync directly (no reflection per
    // dispatch, so handler exceptions surface as themselves rather than TargetInvocationException).
    private sealed class EventDispatch
    {
        private readonly Func<object, object, CancellationToken, Task> _invoke;

        private EventDispatch(Type handlerEnumerableType, Func<object, object, CancellationToken, Task> invoke)
        {
            HandlerEnumerableType = handlerEnumerableType;
            _invoke = invoke;
        }

        public Type HandlerEnumerableType { get; }

        public Task Invoke(object handler, object @event, CancellationToken cancellationToken) =>
            _invoke(handler, @event, cancellationToken);

        public static EventDispatch ForEventType(Type eventType)
        {
            Type handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
            Type enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerType);
            var invoke = (Func<object, object, CancellationToken, Task>)typeof(EventDispatch)
                .GetMethod(nameof(BuildInvoker), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(eventType)
                .Invoke(null, null)!;
            return new EventDispatch(enumerableType, invoke);
        }

        private static Func<object, object, CancellationToken, Task> BuildInvoker<TEvent>()
            where TEvent : class =>
            (handler, @event, cancellationToken) => ((IEventHandler<TEvent>)handler).HandleAsync((TEvent)@event, cancellationToken);
    }
}
