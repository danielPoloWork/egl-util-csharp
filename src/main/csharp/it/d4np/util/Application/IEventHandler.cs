namespace It.D4np.Util.Application;

/// <summary>
/// A consumer of events of type <typeparamref name="TEvent"/> published through an
/// <see cref="IEventBroker"/>.
/// </summary>
/// <typeparam name="TEvent">The handled event type.</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : class
{
    /// <summary>Handles a published event.</summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">A token to cancel handling.</param>
    /// <returns>A task that completes when handling is done.</returns>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
