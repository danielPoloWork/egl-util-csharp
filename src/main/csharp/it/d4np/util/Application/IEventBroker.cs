namespace It.D4np.Util.Application;

/// <summary>
/// An in-memory publish abstraction: hands an event to the broker for asynchronous delivery to its
/// registered <see cref="IEventHandler{TEvent}"/>s, without blocking the caller on handler execution.
/// </summary>
public interface IEventBroker
{
    /// <summary>
    /// Queues <paramref name="event"/> for asynchronous, in-memory delivery to the handlers registered
    /// for its runtime type. The returned task completes once the event has been accepted for delivery,
    /// not once it is handled.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="event">The event to publish; must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel enqueueing (e.g. while waiting under back-pressure).</param>
    /// <returns>A task that completes when the event has been accepted for delivery.</returns>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class;
}
