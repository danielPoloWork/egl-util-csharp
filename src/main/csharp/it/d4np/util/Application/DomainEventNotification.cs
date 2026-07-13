using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

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

/// <summary>
/// Factory helpers for <see cref="DomainEventNotification{TEvent}"/>. Prefer <see cref="Create{TEvent}"/>
/// at call sites that know the event type at compile time (reflection-free, trim/AOT-safe); use
/// <see cref="Create(IDomainEvent)"/> when only the runtime type is available — e.g. iterating the
/// <see cref="IDomainEvent"/>s handed to <c>DbContextExtensions.DispatchDomainEventsAsync</c> — so the
/// notification is closed over the event's <b>runtime</b> type and MediatR resolves the concrete-typed
/// <see cref="INotificationHandler{TNotification}"/>s (publishing <c>DomainEventNotification&lt;IDomainEvent&gt;</c>
/// would reach none of them).
/// </summary>
public static class DomainEventNotification
{
    private static readonly ConcurrentDictionary<Type, Func<IDomainEvent, INotification>> Factories = new();

    /// <summary>
    /// Wraps <paramref name="domainEvent"/> in a <see cref="DomainEventNotification{TEvent}"/> closed over
    /// its <b>compile-time</b> type <typeparamref name="TEvent"/>. Reflection-free and trim/AOT-safe;
    /// prefer this whenever the event type is statically known.
    /// </summary>
    /// <typeparam name="TEvent">The compile-time domain-event type.</typeparam>
    /// <param name="domainEvent">The domain event to wrap; must not be <see langword="null"/>.</param>
    /// <returns>The notification wrapping <paramref name="domainEvent"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    public static DomainEventNotification<TEvent> Create<TEvent>(TEvent domainEvent)
        where TEvent : IDomainEvent => new(domainEvent);

    /// <summary>
    /// Wraps <paramref name="domainEvent"/> in a <see cref="DomainEventNotification{TEvent}"/> closed over
    /// its <b>runtime</b> type, so publishing the result reaches handlers registered for the concrete event
    /// type (e.g. <c>INotificationHandler&lt;DomainEventNotification&lt;OrderPlaced&gt;&gt;</c>). Use this for
    /// collections where only <see cref="IDomainEvent"/> is available; when the concrete type is known at
    /// compile time, prefer the trim/AOT-safe <see cref="Create{TEvent}"/>. The per-event-type constructor
    /// delegate is cached. For Native AOT, use <see cref="Create{TEvent}"/> or register an explicit
    /// per-type factory instead of this reflective path.
    /// </summary>
    /// <param name="domainEvent">The domain event to wrap; must not be <see langword="null"/>.</param>
    /// <returns>The notification, closed over <paramref name="domainEvent"/>'s runtime type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    [RequiresDynamicCode(
        "Closes DomainEventNotification<> over the event's runtime type via MakeGenericType and a compiled " +
        "constructor delegate; not compatible with Native AOT. Use Create<TEvent> or an explicit per-type " +
        "factory in AOT apps.")]
    [RequiresUnreferencedCode(
        "The runtime event type and the DomainEventNotification<> constructor may be trimmed. Use " +
        "Create<TEvent> at statically-known call sites, or preserve the notification types, in trimmed apps.")]
    public static INotification Create(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return Factories.GetOrAdd(domainEvent.GetType(), BuildFactory)(domainEvent);
    }

    [RequiresDynamicCode("Builds and compiles a constructor delegate for the closed notification type.")]
    [RequiresUnreferencedCode("Reflects over the closed DomainEventNotification<> constructor.")]
    private static Func<IDomainEvent, INotification> BuildFactory(Type eventType)
    {
        Type notificationType = typeof(DomainEventNotification<>).MakeGenericType(eventType);
        ConstructorInfo constructor = notificationType.GetConstructor([eventType])
            ?? throw new InvalidOperationException(
                $"DomainEventNotification<{eventType}> has no single-argument constructor.");

        // domainEvent => (INotification) new DomainEventNotification<eventType>((eventType) domainEvent)
        ParameterExpression parameter = Expression.Parameter(typeof(IDomainEvent), "domainEvent");
        NewExpression construction = Expression.New(constructor, Expression.Convert(parameter, eventType));
        return Expression.Lambda<Func<IDomainEvent, INotification>>(construction, parameter).Compile();
    }
}
