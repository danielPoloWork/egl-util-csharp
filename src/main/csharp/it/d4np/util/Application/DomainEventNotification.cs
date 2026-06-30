using It.D4np.Util.Domain;

using MediatR;

namespace It.D4np.Util.Application;

/// <summary>
/// Adapts a domain event to a MediatR <see cref="INotification"/> so it can be published through
/// the application's notification pipeline to any number of registered handlers. This keeps the
/// domain layer (which only raises <see cref="IDomainEvent"/>s) decoupled from the messaging
/// infrastructure.
/// </summary>
/// <typeparam name="TEvent">The wrapped domain-event type.</typeparam>
public sealed class DomainEventNotification<TEvent> : INotification
    where TEvent : IDomainEvent
{
    /// <summary>The wrapped domain event.</summary>
    public TEvent DomainEvent { get; }

    /// <summary>Creates a notification wrapping <paramref name="domainEvent"/>.</summary>
    /// <param name="domainEvent">The domain event to wrap; must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    public DomainEventNotification(TEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        DomainEvent = domainEvent;
    }
}
