using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

using FluentValidation;

using It.D4np.Util.Application;
using It.D4np.Util.Infrastructure;

using MediatR;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace It.D4np.Util.Tests;

public class ServiceCollectionExtensionsTests
{
    // Sample types the assembly scanner should discover in this test assembly.
    private sealed record SampleEvent(string Message);

    private sealed class SampleEventHandler : IEventHandler<SampleEvent>
    {
        public Task HandleAsync(SampleEvent @event, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed record SampleRequest(string Value) : IRequest<string>;

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator() => RuleFor(request => request.Value).NotEmpty();
    }

    private sealed class SampleEntity;

    private sealed class SampleSpecification : ISpecification<SampleEntity>
    {
        public Expression<Func<SampleEntity, bool>>? Criteria => null;
        public IReadOnlyList<Expression<Func<SampleEntity, object>>> Includes => [];
        public IReadOnlyList<string> IncludeStrings => [];
        public Expression<Func<SampleEntity, object>>? OrderBy => null;
        public Expression<Func<SampleEntity, object>>? OrderByDescending => null;
        public int Skip => 0;
        public int Take => 0;
        public bool IsPagingEnabled => false;
    }

    // A specification built with a runtime argument — the normal shape (Specification<TEntity> sets its
    // criteria via the constructor). Regression for #67: the scan must NOT register it, or the container
    // cannot supply the Guid and a ValidateOnBuild host fails to start.
    private sealed class ParameterizedSpecification : Specification<SampleEntity>
    {
        public ParameterizedSpecification(Guid customerId) => _ = customerId;
    }

    // Open-generic implementors in the scanned assembly. Regression for #65: the scan must skip these.
    // GetInterfaces() on the open definition yields the open constructed interface (e.g.
    // IEventHandler<TEvent>), and registering the definition against it makes BuildServiceProvider throw
    // "Cannot instantiate implementation type …" for the whole container — regardless of ValidateOnBuild.
    private sealed class OpenGenericEventHandler<TEvent> : IEventHandler<TEvent>
        where TEvent : class
    {
        public Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class OpenGenericValidator<T> : AbstractValidator<T>;

    // A stand-in IEventBroker a consumer might register to replace the default broker (#92).
    private sealed class CustomBroker : IEventBroker
    {
        public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class => ValueTask.CompletedTask;
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IDistributedCache>(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        services.AddD4npUtil(typeof(ServiceCollectionExtensionsTests).Assembly);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Registers_the_core_services()
    {
        await using ServiceProvider provider = BuildProvider();

        Assert.IsType<DateTimeProvider>(provider.GetRequiredService<IDateTimeProvider>());
        Assert.IsType<PasswordHasher>(provider.GetRequiredService<IPasswordHasher>());
        Assert.IsType<CacheService>(provider.GetRequiredService<ICacheService>());
        Assert.IsType<EventBroker>(provider.GetRequiredService<IEventBroker>());
    }

    [Fact]
    public async Task Resolved_cache_service_works_with_a_size_limited_memory_cache()
    {
        // Regression for #20: a common production registration sets a SizeLimit; the resolved
        // CacheService must not throw on its first read-through write.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions { SizeLimit = 64 }));
        services.AddSingleton<IDistributedCache>(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        services.AddD4npUtil(typeof(ServiceCollectionExtensionsTests).Assembly);
        await using ServiceProvider provider = services.BuildServiceProvider();

        var cache = provider.GetRequiredService<ICacheService>();
        int value = await cache.GetOrCreateAsync("di:size", _ => Task.FromResult(42));

        Assert.Equal(42, value);
    }

    [Fact]
    public async Task Exposes_the_event_broker_as_a_single_hosted_instance()
    {
        await using ServiceProvider provider = BuildProvider();

        var broker = provider.GetRequiredService<IEventBroker>();
        EventBroker hosted = Assert.Single(provider.GetServices<IHostedService>().OfType<EventBroker>());

        Assert.Same(broker, hosted); // the IEventBroker and the hosted service are one instance
    }

    [Fact]
    public async Task Overriding_the_event_broker_disables_the_default_broker_and_its_hosted_service()
    {
        // #92: a consumer that pre-registers its own IEventBroker must fully replace the default broker.
        // The whole cluster is gated on IEventBroker being absent, so the library registers neither its
        // concrete EventBroker nor a hosted service for it — otherwise the default broker would still run
        // as a phantom background service (a consumer loop nothing publishes to) while the replacement
        // stayed un-hosted. The "overridable" contract in the XML remarks is only true with this gate.
        var custom = new CustomBroker();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IDistributedCache>(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        services.AddSingleton<IEventBroker>(custom); // pre-registered override, before AddD4npUtil

        services.AddD4npUtil(typeof(ServiceCollectionExtensionsTests).Assembly);

        await using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(custom, provider.GetRequiredService<IEventBroker>()); // the override wins
        Assert.Null(provider.GetService<EventBroker>()); // the default concrete broker was not registered
        Assert.Empty(provider.GetServices<IHostedService>().OfType<EventBroker>()); // nothing hosts it (no phantom)
    }

    [Fact]
    public async Task Registering_twice_leaves_a_single_hosted_broker()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IDistributedCache>(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        services.AddD4npUtil(typeof(ServiceCollectionExtensionsTests).Assembly);
        services.AddD4npUtil(typeof(ServiceCollectionExtensionsTests).Assembly); // idempotent

        await using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IHostedService>().OfType<EventBroker>());
    }

    [Fact]
    public void Registering_twice_leaves_a_single_validation_behavior()
    {
        // Regression for #32: the open-generic pipeline behavior must be registered idempotently, or a
        // second AddD4npUtil call (library + host) makes MediatR run every validator twice per request.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IDistributedCache>(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        services.AddD4npUtil(typeof(ServiceCollectionExtensionsTests).Assembly);
        services.AddD4npUtil(typeof(ServiceCollectionExtensionsTests).Assembly); // idempotent

        int behaviors = services.Count(descriptor =>
            descriptor.ServiceType == typeof(IPipelineBehavior<,>)
            && descriptor.ImplementationType == typeof(ValidationPipelineBehavior<,>));

        Assert.Equal(1, behaviors); // registered once despite two calls, so validators run once
    }

    [Fact]
    public async Task Core_services_are_singletons()
    {
        await using ServiceProvider provider = BuildProvider();

        Assert.Same(
            provider.GetRequiredService<IDateTimeProvider>(),
            provider.GetRequiredService<IDateTimeProvider>());
    }

    [Fact]
    public async Task Registers_the_validation_pipeline_behavior()
    {
        await using ServiceProvider provider = BuildProvider();

        IEnumerable<IPipelineBehavior<SampleRequest, string>> behaviors =
            provider.GetServices<IPipelineBehavior<SampleRequest, string>>();

        Assert.Contains(behaviors, behavior => behavior is ValidationPipelineBehavior<SampleRequest, string>);
    }

    [Fact]
    public async Task Auto_registers_scanned_handlers_and_validators()
    {
        await using ServiceProvider provider = BuildProvider();

        Assert.IsType<SampleEventHandler>(Assert.Single(provider.GetServices<IEventHandler<SampleEvent>>()));
        Assert.Contains(provider.GetServices<IValidator<SampleRequest>>(), validator => validator is SampleValidator);
    }

    [Fact]
    public async Task Scans_the_calling_assembly_when_no_assembly_is_given()
    {
        // #102: AddD4npUtil() with no arguments falls back to Assembly.GetCallingAssembly() — here the
        // test assembly — and scans it. Every other test passes the assembly explicitly, so this is the
        // only coverage of the documented zero-argument default-scan path.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IDistributedCache>(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        services.AddD4npUtil(); // no assemblies → the calling (test) assembly is scanned

        await using ServiceProvider provider = services.BuildServiceProvider();
        Assert.IsType<SampleEventHandler>(Assert.Single(provider.GetServices<IEventHandler<SampleEvent>>()));
        Assert.Contains(provider.GetServices<IValidator<SampleRequest>>(), validator => validator is SampleValidator);
    }

    [Fact]
    public async Task Specifications_are_not_auto_registered()
    {
        // Regression for #67: specifications are query objects built with runtime arguments (e.g.
        // ParameterizedSpecification(Guid)), so the scan must not register them. Before the fix they were
        // registered as ISpecification<SampleEntity>; a parameterized spec then had no way for the
        // container to supply its Guid, so ValidateOnBuild — which Host/WebApplication builders enable in
        // Development — failed and the whole host could not start. Removing the registration is the fix.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IDistributedCache>(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        services.AddD4npUtil(typeof(ServiceCollectionExtensionsTests).Assembly);

        // No specification is registered — neither the parameterless SampleSpecification nor the
        // parameterized one whose Guid the container could never supply.
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.IsGenericType
            && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(ISpecification<>));

        await using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Empty(provider.GetServices<ISpecification<SampleEntity>>());
    }

    [Fact]
    public async Task Open_generic_implementors_are_skipped_so_the_container_builds()
    {
        // Regression for #65: this assembly defines OpenGenericEventHandler<TEvent> and
        // OpenGenericValidator<T>. Before the fix the scan registered each open definition against its
        // open constructed interface, so BuildServiceProvider threw "Cannot instantiate implementation
        // type …" and the entire application container failed to build. They must be skipped instead.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IDistributedCache>(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        services.AddD4npUtil(typeof(ServiceCollectionExtensionsTests).Assembly);

        // The open generics were not registered at all.
        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == typeof(OpenGenericEventHandler<>));
        Assert.DoesNotContain(services, descriptor => descriptor.ImplementationType == typeof(OpenGenericValidator<>));

        // The container builds (pre-fix this threw) and the ordinary closed handler still resolves.
        await using ServiceProvider provider = services.BuildServiceProvider();
        Assert.IsType<SampleEventHandler>(Assert.Single(provider.GetServices<IEventHandler<SampleEvent>>()));
    }

    [Fact]
    public void AddD4npUtil_rejects_null_arguments()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(
            () => It.D4np.Util.Infrastructure.ServiceCollectionExtensions.AddD4npUtil(null!));
        Assert.Throws<ArgumentNullException>(() => services.AddD4npUtil((Assembly[])null!));
    }

    [Fact]
    public void The_reflective_entry_points_are_annotated_trim_and_AOT_unsafe()
    {
        // #49: the reflective public entry points must carry the trim/AOT annotations so a consumer's
        // trimming or Native-AOT build surfaces the incompatibility at the call site, rather than failing
        // silently at runtime.
        MethodInfo addD4npUtil =
            typeof(It.D4np.Util.Infrastructure.ServiceCollectionExtensions).GetMethod("AddD4npUtil")!;
        Assert.NotNull(addD4npUtil.GetCustomAttribute<RequiresDynamicCodeAttribute>());
        Assert.NotNull(addD4npUtil.GetCustomAttribute<RequiresUnreferencedCodeAttribute>());

        ConstructorInfo brokerCtor = typeof(EventBroker).GetConstructors().Single();
        Assert.NotNull(brokerCtor.GetCustomAttribute<RequiresDynamicCodeAttribute>());
        Assert.NotNull(brokerCtor.GetCustomAttribute<RequiresUnreferencedCodeAttribute>());
    }
}
