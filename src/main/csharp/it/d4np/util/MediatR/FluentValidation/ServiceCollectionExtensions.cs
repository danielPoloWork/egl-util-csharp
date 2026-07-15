using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using global::FluentValidation;

using global::MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace It.D4np.Util.MediatR.FluentValidation;

/// <summary>DI wiring for the FluentValidation pipeline behavior (the opt-in package's validation half, #38).</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="ValidationPipelineBehavior{TRequest, TResponse}"/> as an open-generic MediatR
    /// <see cref="IPipelineBehavior{TRequest, TResponse}"/>, and auto-registers every concrete
    /// <see cref="IValidator{T}"/> discovered in <paramref name="assemblies"/> as transient. Open-generic
    /// definitions are skipped (DI cannot construct them). Idempotent — <c>TryAddEnumerable</c> keeps a
    /// repeated call from registering the behavior twice (running every validator twice per request). When no
    /// assembly is given, the assembly that directly calls this method is scanned.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="assemblies">The assemblies to scan for validators; defaults to the calling assembly.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="assemblies"/> is <see langword="null"/>.</exception>
    [RequiresDynamicCode(
        "Registers ValidationPipelineBehavior<,> as an open-generic pipeline behavior that DI closes per " +
        "request via MakeGenericType — not compatible with Native AOT.")]
    [RequiresUnreferencedCode(
        "Assembly-scans for IValidator<> implementations via Assembly.GetTypes; the discovered validator " +
        "types may be trimmed.")]
    // NoInlining so Assembly.GetCallingAssembly() resolves the caller, not an inlined caller's caller (#90).
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddD4npUtilMediatRValidation(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        Assembly[] toScan = assemblies.Length > 0 ? assemblies : [Assembly.GetCallingAssembly()];

        // Run FluentValidation validators as a MediatR pipeline step. TryAddEnumerable (keyed on the
        // implementation type) keeps this idempotent across repeated calls.
        services.TryAddEnumerable(
            ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>)));

        RegisterValidators(services, toScan);
        return services;
    }

    // Registers every concrete IValidator<> implementation in the assemblies as transient. Open-generic
    // definitions and abstractions are skipped: their GetInterfaces() yields the open constructed interface,
    // and registering the definition against it would make BuildServiceProvider throw for the whole container.
    private static void RegisterValidators(IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        foreach (Type type in assemblies.SelectMany(GetLoadableTypes))
        {
            if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
            {
                continue;
            }

            foreach (Type @interface in type.GetInterfaces())
            {
                if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IValidator<>))
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
