namespace It.D4np.Util.Infrastructure;

/// <summary>
/// The concrete event-handler types discovered by <see cref="ServiceCollectionExtensions.AddD4npUtil"/>'s
/// assembly scan, indexed by the closed <see cref="It.D4np.Util.Application.IEventHandler{TEvent}"/>
/// interface each one implements. <see cref="EventBroker"/> uses this to reconstruct handlers <b>one at a
/// time</b> when the container's eager <c>IEnumerable&lt;IEventHandler&lt;T&gt;&gt;</c> resolution throws on a
/// single handler's constructor — so one faulty handler's construction no longer drops the event for its
/// healthy siblings (#89, ADR-0038).
/// </summary>
/// <remarks>
/// Populated only during registration (single-threaded application startup) and read-only thereafter, so it
/// needs no synchronization. It records exactly the (closed handler interface → implementation type) pairs
/// the scan registers, deduplicated, in scan order.
/// </remarks>
internal sealed class EventHandlerRegistry
{
    private static readonly IReadOnlyList<Type> None = [];

    private readonly Dictionary<Type, List<Type>> _implementationsByHandlerInterface = [];

    /// <summary>
    /// Records that <paramref name="implementationType"/> is a handler for the closed
    /// <paramref name="handlerInterfaceType"/> (an <c>IEventHandler&lt;T&gt;</c>). Idempotent per pair.
    /// Registration-time only.
    /// </summary>
    public void Add(Type handlerInterfaceType, Type implementationType)
    {
        if (!_implementationsByHandlerInterface.TryGetValue(handlerInterfaceType, out List<Type>? implementations))
        {
            implementations = [];
            _implementationsByHandlerInterface[handlerInterfaceType] = implementations;
        }

        if (!implementations.Contains(implementationType))
        {
            implementations.Add(implementationType);
        }
    }

    /// <summary>
    /// The concrete handler types registered for the closed <paramref name="handlerInterfaceType"/>, in
    /// scan order; empty when none were scanned for it.
    /// </summary>
    public IReadOnlyList<Type> ImplementationsFor(Type handlerInterfaceType) =>
        _implementationsByHandlerInterface.TryGetValue(handlerInterfaceType, out List<Type>? implementations)
            ? implementations
            : None;
}
