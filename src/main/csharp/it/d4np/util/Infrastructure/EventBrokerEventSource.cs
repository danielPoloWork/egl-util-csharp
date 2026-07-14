using System.Diagnostics.Tracing;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// An <see cref="EventSource"/> that reports <see cref="EventBroker"/> handler and dispatch failures,
/// independently of any <c>ILogger</c>. It is always available (ETW / <c>dotnet-trace</c> /
/// <see cref="EventListener"/>), so a throwing handler is never silently dropped even when the broker was
/// constructed without a logger (#43). Listen on the source name <c>It.D4np.Util.EventBroker</c>.
/// </summary>
[EventSource(Name = SourceName)]
internal sealed class EventBrokerEventSource : EventSource
{
    /// <summary>The ETW/EventListener source name to subscribe to.</summary>
    public const string SourceName = "It.D4np.Util.EventBroker";

    /// <summary>The process-wide singleton, following the EventSource convention.</summary>
    public static readonly EventBrokerEventSource Log = new();

    private EventBrokerEventSource()
    {
    }

    /// <summary>Reports that an event handler threw and was isolated (the event was still dispatched to its siblings).</summary>
    [Event(1, Level = EventLevel.Error, Message = "Event handler '{0}' failed for event '{1}'.")]
    public void HandlerFailed(string handlerType, string eventType, string exception)
    {
        if (IsEnabled())
        {
            WriteEvent(1, handlerType, eventType, exception);
        }
    }

    /// <summary>Reports that dispatching an event failed outside any single handler (a last-resort isolation).</summary>
    [Event(2, Level = EventLevel.Error, Message = "Failed to dispatch event '{0}'.")]
    public void DispatchFailed(string eventType, string exception)
    {
        if (IsEnabled())
        {
            WriteEvent(2, eventType, exception);
        }
    }
}
