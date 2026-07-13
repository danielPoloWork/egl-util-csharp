using It.D4np.Util.Application;
using It.D4np.Util.Domain;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

namespace It.D4np.Util.Tests;

public class DomainEventNotificationTests
{
    private sealed record Registered(DateTime OccurredOnUtc) : IDomainEvent;

    private sealed record OrderPlaced(DateTime OccurredOnUtc) : IDomainEvent;

    private sealed class Sink
    {
        public List<Type> Received { get; } = [];
    }

    private sealed class OrderPlacedHandler(Sink sink) : INotificationHandler<DomainEventNotification<OrderPlaced>>
    {
        public Task Handle(DomainEventNotification<OrderPlaced> notification, CancellationToken cancellationToken)
        {
            sink.Received.Add(notification.DomainEvent.GetType());
            return Task.CompletedTask;
        }
    }

    // A mediator with exactly one handler — INotificationHandler<DomainEventNotification<OrderPlaced>> —
    // registered explicitly (MediatR scans only its own assembly, so the handler is registered once).
    private static (IMediator Mediator, Sink Sink) BuildMediator()
    {
        var sink = new Sink();
        var services = new ServiceCollection();
        services.AddSingleton(sink);
        services.AddSingleton<INotificationHandler<DomainEventNotification<OrderPlaced>>>(_ => new OrderPlacedHandler(sink));
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(IMediator).Assembly));
        ServiceProvider provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMediator>(), sink);
    }

    [Fact]
    public void Wraps_the_domain_event()
    {
        var domainEvent = new Registered(DateTime.UnixEpoch);

        var notification = new DomainEventNotification<Registered>(domainEvent);

        Assert.Same(domainEvent, notification.DomainEvent);
    }

    [Fact]
    public void Is_a_mediatr_notification()
    {
        var notification = new DomainEventNotification<Registered>(new Registered(DateTime.UnixEpoch));

        Assert.IsAssignableFrom<INotification>(notification);
    }

    [Fact]
    public void Rejects_a_null_event()
    {
        Assert.Throws<ArgumentNullException>(() => new DomainEventNotification<Registered>(null!));
    }

    [Fact]
    public async Task Create_from_a_runtime_typed_event_reaches_the_concrete_typed_handler()
    {
        // #61: the primary usage — a callback that only has an IDomainEvent (e.g. DispatchDomainEventsAsync).
        // Create closes the notification over the RUNTIME type, so the concrete-typed handler fires.
        (IMediator mediator, Sink sink) = BuildMediator();
        IDomainEvent domainEvent = new OrderPlaced(DateTime.UnixEpoch);

        await mediator.Publish(DomainEventNotification.Create(domainEvent));

        Assert.Equal([typeof(OrderPlaced)], sink.Received);
    }

    [Fact]
    public async Task Create_generic_reaches_the_concrete_typed_handler()
    {
        // The compile-time-typed overload (reflection-free) resolves the same concrete handler.
        (IMediator mediator, Sink sink) = BuildMediator();

        DomainEventNotification<OrderPlaced> notification =
            DomainEventNotification.Create(new OrderPlaced(DateTime.UnixEpoch));

        await mediator.Publish(notification);

        Assert.Equal([typeof(OrderPlaced)], sink.Received);
    }

    [Fact]
    public async Task The_open_typed_notification_reaches_no_concrete_typed_handler()
    {
        // The trap #61 fixes: wrapping as DomainEventNotification<IDomainEvent> publishes the wrong closed
        // type, so INotificationHandler<DomainEventNotification<OrderPlaced>> silently never fires.
        (IMediator mediator, Sink sink) = BuildMediator();

        await mediator.Publish(new DomainEventNotification<IDomainEvent>(new OrderPlaced(DateTime.UnixEpoch)));

        Assert.Empty(sink.Received);
    }

    [Fact]
    public void Create_rejects_a_null_event()
    {
        Assert.Throws<ArgumentNullException>(() => DomainEventNotification.Create((IDomainEvent)null!));
        Assert.Throws<ArgumentNullException>(() => DomainEventNotification.Create<OrderPlaced>(null!));
    }
}
