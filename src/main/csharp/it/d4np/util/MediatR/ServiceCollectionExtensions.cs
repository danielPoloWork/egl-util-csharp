using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using It.D4np.Util.MediatR.FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace It.D4np.Util.MediatR;

/// <summary>Convenience DI wiring for the opt-in <c>It.D4np.Util.MediatR</c> building blocks (#38).</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enables the package's MediatR building blocks in one call: registers the FluentValidation
    /// <c>ValidationPipelineBehavior</c> and scans <paramref name="assemblies"/> for validators (via
    /// <see cref="It.D4np.Util.MediatR.FluentValidation.ServiceCollectionExtensions.AddD4npUtilMediatRValidation"/>).
    /// The <see cref="DomainEventNotification{TEvent}"/> adapter is a type/factory used directly and needs no
    /// registration, so there is nothing further to wire. Call the granular extension instead if you want
    /// only the validation behavior. When no assembly is given, the assembly that directly calls this method
    /// is scanned.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="assemblies">The assemblies to scan for validators; defaults to the calling assembly.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="assemblies"/> is <see langword="null"/>.</exception>
    [RequiresDynamicCode(
        "Delegates to AddD4npUtilMediatRValidation, which registers an open-generic pipeline behavior DI " +
        "closes via MakeGenericType — not compatible with Native AOT.")]
    [RequiresUnreferencedCode(
        "Delegates to AddD4npUtilMediatRValidation, which assembly-scans for IValidator<> via " +
        "Assembly.GetTypes; the discovered validator types may be trimmed.")]
    // NoInlining so Assembly.GetCallingAssembly() resolves the consumer, then passes it explicitly to the
    // validation registration (which would otherwise see THIS assembly as the caller) (#90).
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddD4npUtilMediatR(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        Assembly[] toScan = assemblies.Length > 0 ? assemblies : [Assembly.GetCallingAssembly()];
        return services.AddD4npUtilMediatRValidation(toScan);
    }
}
