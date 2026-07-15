using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
// global:: — It.D4np.Util.MediatR(.FluentValidation) shadows the packages from under the It.D4np.Util.* tree.
using global::FluentValidation;
using global::MediatR;

using It.D4np.Util.MediatR;
using It.D4np.Util.MediatR.FluentValidation;

using Microsoft.Extensions.DependencyInjection;

namespace It.D4np.Util.Tests;

// DI wiring for the opt-in It.D4np.Util.MediatR package (#38): the FluentValidation pipeline behavior
// registration and the convenience umbrella. The core ServiceCollectionExtensionsTests asserts the reverse —
// that AddD4npUtil registers none of this and the core assemblies carry no MediatR/FluentValidation reference.
public class MediatRServiceCollectionExtensionsTests
{
    // Sample types the validator scan should discover in this test assembly.
    private sealed record SampleRequest(string Value) : IRequest<string>;

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator() => RuleFor(request => request.Value).NotEmpty();
    }

    // An open-generic validator. Regression for #65 (validator half): the scan must skip it — registering
    // the open definition against its open constructed IValidator<T> makes BuildServiceProvider throw
    // "Cannot instantiate implementation type …" for the whole container.
    private sealed class OpenGenericValidator<T> : AbstractValidator<T>;

    private static readonly Assembly ThisAssembly = typeof(MediatRServiceCollectionExtensionsTests).Assembly;

    [Fact]
    public void Registers_the_validation_pipeline_behavior()
    {
        var services = new ServiceCollection();
        services.AddD4npUtilMediatRValidation(ThisAssembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        IEnumerable<IPipelineBehavior<SampleRequest, string>> behaviors =
            provider.GetServices<IPipelineBehavior<SampleRequest, string>>();

        Assert.Contains(behaviors, behavior => behavior is ValidationPipelineBehavior<SampleRequest, string>);
    }

    [Fact]
    public void Registering_twice_leaves_a_single_validation_behavior()
    {
        // Regression for #32: TryAddEnumerable keeps the open-generic behavior from being registered twice —
        // otherwise a library and its host both calling this makes MediatR run every validator twice per request.
        var services = new ServiceCollection();
        services.AddD4npUtilMediatRValidation(ThisAssembly);
        services.AddD4npUtilMediatRValidation(ThisAssembly); // idempotent

        int behaviors = services.Count(descriptor =>
            descriptor.ServiceType == typeof(IPipelineBehavior<,>)
            && descriptor.ImplementationType == typeof(ValidationPipelineBehavior<,>));

        Assert.Equal(1, behaviors); // registered once despite two calls, so validators run once
    }

    [Fact]
    public void Auto_registers_scanned_validators()
    {
        var services = new ServiceCollection();
        services.AddD4npUtilMediatRValidation(ThisAssembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Contains(provider.GetServices<IValidator<SampleRequest>>(), validator => validator is SampleValidator);
    }

    [Fact]
    public void Open_generic_validators_are_skipped_so_the_container_builds()
    {
        // Regression for #65 (validator half): OpenGenericValidator<T> must be skipped. GetInterfaces() on the
        // open definition yields the open constructed IValidator<T>, and registering the definition against it
        // makes BuildServiceProvider throw "Cannot instantiate implementation type …" for the whole container.
        var services = new ServiceCollection();
        services.AddD4npUtilMediatRValidation(ThisAssembly);

        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == typeof(OpenGenericValidator<>));

        // The container builds (pre-fix this threw) and the ordinary closed validator still resolves.
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Contains(provider.GetServices<IValidator<SampleRequest>>(), validator => validator is SampleValidator);
    }

    [Fact]
    public void Scans_the_calling_assembly_when_no_assembly_is_given()
    {
        // The zero-argument default scan resolves the calling (test) assembly via Assembly.GetCallingAssembly().
        var services = new ServiceCollection();
        services.AddD4npUtilMediatRValidation(); // no assemblies → the calling (test) assembly is scanned

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Contains(provider.GetServices<IValidator<SampleRequest>>(), validator => validator is SampleValidator);
    }

    [Fact]
    public void AddD4npUtilMediatR_enables_the_validation_behavior_and_scans_validators()
    {
        // The convenience umbrella delegates to AddD4npUtilMediatRValidation (the DomainEventNotification
        // adapter is a type/factory used directly and needs no registration), so it must register the behavior
        // and scan validators just the same.
        var services = new ServiceCollection();
        services.AddD4npUtilMediatR(ThisAssembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Contains(
            provider.GetServices<IPipelineBehavior<SampleRequest, string>>(),
            behavior => behavior is ValidationPipelineBehavior<SampleRequest, string>);
        Assert.Contains(provider.GetServices<IValidator<SampleRequest>>(), validator => validator is SampleValidator);
    }

    [Fact]
    public void AddD4npUtilMediatR_scans_the_calling_assembly_when_no_assembly_is_given()
    {
        // #90: the umbrella resolves GetCallingAssembly() itself and passes it explicitly to the validation
        // registration, so the scan targets the caller (test) assembly — not the package assembly.
        var services = new ServiceCollection();
        services.AddD4npUtilMediatR();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Contains(provider.GetServices<IValidator<SampleRequest>>(), validator => validator is SampleValidator);
    }

    [Fact]
    public void The_entry_points_reject_null_arguments()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            It.D4np.Util.MediatR.FluentValidation.ServiceCollectionExtensions.AddD4npUtilMediatRValidation(null!));
        Assert.Throws<ArgumentNullException>(() => services.AddD4npUtilMediatRValidation((Assembly[])null!));

        Assert.Throws<ArgumentNullException>(() =>
            It.D4np.Util.MediatR.ServiceCollectionExtensions.AddD4npUtilMediatR(null!));
        Assert.Throws<ArgumentNullException>(() => services.AddD4npUtilMediatR((Assembly[])null!));
    }

    [Fact]
    public void The_reflective_entry_points_are_annotated_trim_and_AOT_unsafe()
    {
        // #49: the reflective scan entry points must carry the trim/AOT annotations — they assembly-scan for
        // IValidator<> via Assembly.GetTypes and register an open-generic behavior DI closes via MakeGenericType.
        foreach (MethodInfo entryPoint in ReflectiveEntryPoints())
        {
            Assert.NotNull(entryPoint.GetCustomAttribute<RequiresDynamicCodeAttribute>());
            Assert.NotNull(entryPoint.GetCustomAttribute<RequiresUnreferencedCodeAttribute>());
        }
    }

    [Fact]
    public void The_default_scan_entry_points_are_not_inlinable()
    {
        // #90: both read Assembly.GetCallingAssembly() for the zero-argument default scan; NoInlining keeps the
        // JIT from folding the frame away and resolving the wrong assembly (silently scanning nothing).
        foreach (MethodInfo entryPoint in ReflectiveEntryPoints())
        {
            Assert.True(entryPoint.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining));
        }
    }

    private static MethodInfo[] ReflectiveEntryPoints() =>
    [
        typeof(It.D4np.Util.MediatR.FluentValidation.ServiceCollectionExtensions)
            .GetMethod("AddD4npUtilMediatRValidation")!,
        typeof(It.D4np.Util.MediatR.ServiceCollectionExtensions).GetMethod("AddD4npUtilMediatR")!,
    ];
}
