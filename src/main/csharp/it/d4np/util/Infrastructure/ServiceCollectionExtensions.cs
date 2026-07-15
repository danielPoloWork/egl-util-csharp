using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using It.D4np.Util.Application;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace It.D4np.Util.Infrastructure;

/// <summary>
/// Dependency-injection wiring for the egl-util-csharp building blocks. <see cref="AddD4npUtil"/>
/// registers the library's own services and assembly-scans the given assemblies to auto-register the
/// consumer's event handlers and FluentValidation validators.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the egl-util-csharp services and auto-registers the event handlers and validators
    /// discovered in <paramref name="assemblies"/>. Call this once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registered as singletons (overridable — see below): <see cref="TimeProvider.System"/> (the BCL
    /// clock), <see cref="IDateTimeProvider"/> → <see cref="DateTimeProvider"/> (which delegates to that
    /// <see cref="TimeProvider"/>), <see cref="IPasswordHasher"/> → <see cref="PasswordHasher"/>,
    /// <see cref="ICacheService"/> → <see cref="CacheService"/>, and <see cref="IEventBroker"/> →
    /// <see cref="EventBroker"/> (the same instance also runs as an <c>IHostedService</c>). The
    /// MediatR/FluentValidation building blocks (the domain-event notification adapter and the validation
    /// pipeline behavior) are <em>not</em> registered here — they live in the opt-in
    /// <c>It.D4np.Util.MediatR</c> package (call its <c>AddD4npUtilMediatR</c>) so the core imposes neither
    /// dependency (#38).
    /// </para>
    /// <para>
    /// <b>Overriding a default.</b> Register your own implementation <em>before</em> calling this: the core
    /// services use <c>TryAdd</c>, so the first registration wins. Overriding <see cref="IEventBroker"/>
    /// disables the default broker <em>entirely</em> — the concrete <see cref="EventBroker"/> and its
    /// <c>IHostedService</c> are registered only when no <see cref="IEventBroker"/> is already present, so a
    /// replacement never leaves the default broker running as a phantom background service (#92). If you want
    /// your replacement broker hosted, register it as an <c>IHostedService</c> yourself.
    /// </para>
    /// <para>
    /// The consumer supplies the runtime dependencies this wiring assumes: an
    /// <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> and an
    /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> (for
    /// <see cref="CacheService"/>) and — for event delivery — logging. Assembly scanning registers every
    /// concrete <see cref="IEventHandler{TEvent}"/> as transient; open-generic type definitions are skipped
    /// (DI cannot construct them). When no assembly is given, the assembly that <em>directly</em> calls this method is
    /// scanned (via <see cref="Assembly.GetCallingAssembly"/>; the method is marked
    /// <see cref="MethodImplOptions.NoInlining"/> so the JIT cannot fold that frame away and resolve the
    /// wrong assembly — #90). Note this means a wrapper: a shared-library convenience/extension method that
    /// forwards to <c>AddD4npUtil()</c> makes the <em>wrapper's</em> assembly the scan target, not the
    /// application's. From such a wrapper, pass the intended assemblies explicitly.
    /// </para>
    /// <para>
    /// <see cref="ISpecification{TEntity}"/> implementations are intentionally <em>not</em> registered.
    /// A specification is a query object constructed with its runtime arguments (see
    /// <see cref="Specification{TEntity}"/>, whose criteria is set through the constructor), so a
    /// container registration could not supply those parameters and would break
    /// <c>ValidateOnBuild</c>; new one up where you use it instead.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="assemblies">The assemblies to scan; defaults to the calling assembly when empty.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="assemblies"/> is <see langword="null"/>.
    /// </exception>
    [RequiresDynamicCode(
        "Registers the EventBroker, whose runtime-type dispatch closes generic handler types via " +
        "MakeGenericType/MakeGenericMethod — not compatible with Native AOT.")]
    [RequiresUnreferencedCode(
        "Assembly-scans for IEventHandler<> and IValidator<> implementations via Assembly.GetTypes; the " +
        "discovered handler and validator types may be trimmed.")]
    // The no-argument default scan uses Assembly.GetCallingAssembly(); NoInlining keeps this frame on the
    // stack so the JIT cannot inline the method into its caller and make GetCallingAssembly() resolve the
    // caller's caller (silently scanning the wrong assembly and registering nothing) (#90).
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddD4npUtil(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        Assembly[] toScan = assemblies.Length > 0 ? assemblies : [Assembly.GetCallingAssembly()];

        // Register the BCL clock (TimeProvider.System) so consumers can inject TimeProvider directly, and
        // the default DateTimeProvider (below) delegates to the same instance — both clocks agree (#97).
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.TryAddSingleton<IPasswordHasher, PasswordHasher>();
        services.TryAddSingleton<ICacheService, CacheService>();

        // One EventBroker instance, exposed as IEventBroker and run as a hosted service. The whole cluster —
        // the concrete broker, the IEventBroker mapping, AND the hosted service that runs it — is gated on
        // IEventBroker being absent, so a consumer overrides the broker by registering their own IEventBroker
        // BEFORE calling this. Gating only the mapping (TryAdd) would still register and START the default
        // broker as a hosted service — a phantom consumer loop nothing publishes to — while leaving the
        // replacement un-hosted; the override must disable the default broker entirely (#92). The gate also
        // makes the registrations idempotent across repeated AddD4npUtil calls (the first call adds the
        // mapping, so a second finds it present and skips).
        if (services.All(static descriptor => descriptor.ServiceType != typeof(IEventBroker)))
        {
            services.TryAddSingleton<EventBroker>();
            services.TryAddSingleton<IEventBroker>(static provider => provider.GetRequiredService<EventBroker>());
            services.AddHostedService(static provider => provider.GetRequiredService<EventBroker>());
        }

        // The MediatR FluentValidation pipeline behavior and validator scan live in the opt-in
        // It.D4np.Util.MediatR package (AddD4npUtilMediatRValidation) so the core imposes neither MediatR
        // nor FluentValidation (#38).

        // Specifications are deliberately NOT scanned: they are query objects built with runtime
        // arguments (Specification<TEntity> sets its criteria via the constructor), so a DI registration
        // cannot supply those parameters and a parameterized spec breaks ValidateOnBuild (#67). Consumers
        // new up a specification where they use it.

        // Record the scanned handler types in a registry (accumulated across calls, registered once) so the
        // broker can reconstruct handlers one at a time — isolating a construction failure from its healthy
        // siblings — when the container's eager IEnumerable<IEventHandler<T>> resolution throws on one
        // handler's constructor (#89, ADR-0038).
        EventHandlerRegistry handlerRegistry = GetOrAddEventHandlerRegistry(services);
        RegisterImplementations(services, typeof(IEventHandler<>), toScan, handlerRegistry);

        return services;
    }

    // Registers every concrete type in the assemblies that closes the given open-generic interface, as a
    // transient mapping from the closed interface to the implementation, and records the same pair in
    // <paramref name="registry"/> (for the broker's per-handler construction fallback, #89). Open-generic
    // type definitions are skipped: DI cannot construct them, so registering one would abort the whole
    // container build (see below).
    private static void RegisterImplementations(
        IServiceCollection services, Type openGenericInterface, IEnumerable<Assembly> assemblies, EventHandlerRegistry registry)
    {
        foreach (Type type in assemblies.SelectMany(GetLoadableTypes))
        {
            // Skip open-generic definitions (e.g. `AuditHandler<TEvent> : IEventHandler<TEvent>`) as well
            // as abstractions. Their GetInterfaces() yields the open constructed interface
            // (`IEventHandler`1[TEvent]`), which still matches the target below, so registering the type
            // against it would make BuildServiceProvider throw ("Cannot instantiate implementation type …")
            // for the entire application. A handler must be a closed, new-able type to be auto-registered.
            if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
            {
                continue;
            }

            foreach (Type @interface in type.GetInterfaces())
            {
                if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == openGenericInterface)
                {
                    services.TryAddEnumerable(ServiceDescriptor.Transient(@interface, type));

                    // Mirror the registration into the registry so the broker can reconstruct this handler
                    // individually if the eager IEnumerable resolution fails on a sibling's ctor (#89).
                    registry.Add(@interface, type);
                }
            }
        }
    }

    // Returns the EventHandlerRegistry already registered in the collection, or creates and registers a new
    // one. Idempotent across repeated AddD4npUtil calls: the single registry instance accumulates every
    // call's scanned handler types (the descriptor is added once; later calls merge into the same instance).
    private static EventHandlerRegistry GetOrAddEventHandlerRegistry(IServiceCollection services)
    {
        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.ServiceType == typeof(EventHandlerRegistry)
                && descriptor.ImplementationInstance is EventHandlerRegistry existing)
            {
                return existing;
            }
        }

        var registry = new EventHandlerRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    // Tolerates assemblies with a few unloadable types rather than failing the whole scan.
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(static type => type is not null)!;
        }
    }
}
