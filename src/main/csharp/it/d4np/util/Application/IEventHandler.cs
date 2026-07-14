namespace It.D4np.Util.Application;

/// <summary>
/// A consumer of events of type <typeparamref name="TEvent"/> published through an
/// <see cref="IEventBroker"/>. The type parameter is <b>contravariant</b>: a handler registered for a
/// base type or interface also receives derived events, so an <c>IEventHandler&lt;IDomainEvent&gt;</c>
/// observes every published event. The <c>EventBroker</c> honors this by dispatching an event to the
/// handlers of its runtime type and of each of its base types/interfaces.
/// </summary>
/// <typeparam name="TEvent">The handled event type; a handler also receives any type assignable to it.</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : class
{
    /// <summary>Handles a published event.</summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">A token to cancel handling.</param>
    /// <returns>A task that completes when handling is done.</returns>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
