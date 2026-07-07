using System.Reflection;

using FluentValidation;

using It.D4np.Util.Application;

using MediatR;

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
    /// Registered as singletons (overridable — each uses <c>TryAdd</c>): <see cref="IDateTimeProvider"/>
    /// → <see cref="DateTimeProvider"/>, <see cref="IPasswordHasher"/> → <see cref="PasswordHasher"/>,
    /// <see cref="ICacheService"/> → <see cref="CacheService"/>, and <see cref="IEventBroker"/> →
    /// <see cref="EventBroker"/> (the same instance also runs as an <c>IHostedService</c>). The
    /// <see cref="ValidationPipelineBehavior{TRequest, TResponse}"/> is registered as an open-generic
    /// MediatR <see cref="IPipelineBehavior{TRequest, TResponse}"/>.
    /// </para>
    /// <para>
    /// The consumer supplies the runtime dependencies this wiring assumes: an
    /// <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> and an
    /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> (for
    /// <see cref="CacheService"/>), MediatR itself, and — for event delivery — logging. Assembly
    /// scanning registers every concrete <see cref="IEventHandler{TEvent}"/> and
    /// <see cref="IValidator{T}"/> as transient; open-generic type definitions are skipped (DI cannot
    /// construct them). When no assembly is given, the calling assembly is scanned.
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
    public static IServiceCollection AddD4npUtil(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        Assembly[] toScan = assemblies.Length > 0 ? assemblies : [Assembly.GetCallingAssembly()];

        services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.TryAddSingleton<IPasswordHasher, PasswordHasher>();
        services.TryAddSingleton<ICacheService, CacheService>();

        // One EventBroker instance, exposed as IEventBroker and run as a hosted service. AddHostedService's
        // factory overload uses TryAddEnumerable keyed on EventBroker, so it is idempotent across repeated
        // calls while still resolving the single registered EventBroker.
        services.TryAddSingleton<EventBroker>();
        services.TryAddSingleton<IEventBroker>(static provider => provider.GetRequiredService<EventBroker>());
        services.AddHostedService(static provider => provider.GetRequiredService<EventBroker>());

        // Run FluentValidation validators as a MediatR pipeline step. TryAddEnumerable (keyed on the
        // implementation type) keeps this idempotent, so calling AddD4npUtil more than once — e.g. a
        // library and its host both call it — does not register the behavior twice and run every
        // validator twice per request.
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>)));

        // Specifications are deliberately NOT scanned: they are query objects built with runtime
        // arguments (Specification<TEntity> sets its criteria via the constructor), so a DI registration
        // cannot supply those parameters and a parameterized spec breaks ValidateOnBuild (#67). Consumers
        // new up a specification where they use it.
        RegisterImplementations(services, typeof(IEventHandler<>), toScan);
        RegisterImplementations(services, typeof(IValidator<>), toScan);

        return services;
    }

    // Registers every concrete type in the assemblies that closes the given open-generic interface,
    // as a transient mapping from the closed interface to the implementation. Open-generic type
    // definitions are skipped: DI cannot construct them, so registering one would abort the whole
    // container build (see below).
    private static void RegisterImplementations(
        IServiceCollection services, Type openGenericInterface, IEnumerable<Assembly> assemblies)
    {
        foreach (Type type in assemblies.SelectMany(GetLoadableTypes))
        {
            // Skip open-generic definitions (e.g. `AuditHandler<TEvent> : IEventHandler<TEvent>`) as well
            // as abstractions. Their GetInterfaces() yields the open constructed interface
            // (`IEventHandler`1[TEvent]`), which still matches the target below, so registering the type
            // against it would make BuildServiceProvider throw ("Cannot instantiate implementation type …")
            // for the entire application. MediatR and FluentValidation skip generic definitions likewise; a
            // handler or validator must be a closed, new-able type to be auto-registered.
            if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
            {
                continue;
            }

            foreach (Type @interface in type.GetInterfaces())
            {
                if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == openGenericInterface)
                {
                    services.TryAddEnumerable(ServiceDescriptor.Transient(@interface, type));
                }
            }
        }
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
