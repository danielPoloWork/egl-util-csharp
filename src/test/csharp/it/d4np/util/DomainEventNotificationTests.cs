using It.D4np.Util.Application;
using It.D4np.Util.Domain;

using MediatR;

namespace It.D4np.Util.Tests;

public class DomainEventNotificationTests
{
    private sealed record Registered(DateTime OccurredOnUtc) : IDomainEvent;

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
}
