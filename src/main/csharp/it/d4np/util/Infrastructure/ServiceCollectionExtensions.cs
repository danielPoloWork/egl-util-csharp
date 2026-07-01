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
/// consumer's event handlers, FluentValidation validators, and specifications.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the egl-util-csharp services and auto-registers the handlers, validators, and
    /// specifications discovered in <paramref name="assemblies"/>. Call this once.
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
    /// scanning registers every concrete
    /// <see cref="IEventHandler{TEvent}"/>, <see cref="IValidator{T}"/>, and
    /// <see cref="ISpecification{TEntity}"/> as transient. When no assembly is given, the calling
    /// assembly is scanned.
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

        // Run FluentValidation validators as a MediatR pipeline step.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));

        RegisterImplementations(services, typeof(IEventHandler<>), toScan);
        RegisterImplementations(services, typeof(IValidator<>), toScan);
        RegisterImplementations(services, typeof(ISpecification<>), toScan);

        return services;
    }

    // Registers every concrete type in the assemblies that closes the given open-generic interface,
    // as a transient mapping from the closed interface to the implementation.
    private static void RegisterImplementations(
        IServiceCollection services, Type openGenericInterface, IEnumerable<Assembly> assemblies)
    {
        foreach (Type type in assemblies.SelectMany(GetLoadableTypes))
        {
            if (type.IsAbstract || type.IsInterface)
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
