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
    public async Task Exposes_the_event_broker_as_a_single_hosted_instance()
    {
        await using ServiceProvider provider = BuildProvider();

        var broker = provider.GetRequiredService<IEventBroker>();
        EventBroker hosted = Assert.Single(provider.GetServices<IHostedService>().OfType<EventBroker>());

        Assert.Same(broker, hosted); // the IEventBroker and the hosted service are one instance
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
    public async Task Auto_registers_scanned_handlers_validators_and_specifications()
    {
        await using ServiceProvider provider = BuildProvider();

        Assert.IsType<SampleEventHandler>(Assert.Single(provider.GetServices<IEventHandler<SampleEvent>>()));
        Assert.Contains(provider.GetServices<IValidator<SampleRequest>>(), validator => validator is SampleValidator);
        Assert.IsType<SampleSpecification>(Assert.Single(provider.GetServices<ISpecification<SampleEntity>>()));
    }

    [Fact]
    public void AddD4npUtil_rejects_null_arguments()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(
            () => It.D4np.Util.Infrastructure.ServiceCollectionExtensions.AddD4npUtil(null!));
        Assert.Throws<ArgumentNullException>(() => services.AddD4npUtil((Assembly[])null!));
    }
}
