using It.D4np.Util.Domain;

namespace It.D4np.Util.Tests;

public class AggregateRootTests
{
    private sealed record Registered(DateTime OccurredOnUtc) : IDomainEvent;

    private sealed class Account(Guid id) : AggregateRoot<Guid, Account>(id)
    {
        public void Record(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
    }

    [Fact]
    public void A_new_aggregate_has_no_domain_events()
    {
        var account = new Account(Guid.NewGuid());

        Assert.Empty(account.DomainEvents);
    }

    [Fact]
    public void Raised_events_are_exposed_in_the_order_raised()
    {
        var account = new Account(Guid.NewGuid());
        var first = new Registered(DateTime.UnixEpoch);
        var second = new Registered(DateTime.UnixEpoch.AddDays(1));

        account.Record(first);
        account.Record(second);

        Assert.Equal(new IDomainEvent[] { first, second }, account.DomainEvents);
    }

    [Fact]
    public void Clearing_removes_all_buffered_events()
    {
        var account = new Account(Guid.NewGuid());
        account.Record(new Registered(DateTime.UnixEpoch));

        account.ClearDomainEvents();

        Assert.Empty(account.DomainEvents);
    }

    [Fact]
    public void The_exposed_collection_is_read_only()
    {
        var account = new Account(Guid.NewGuid());

        Assert.IsAssignableFrom<IReadOnlyCollection<IDomainEvent>>(account.DomainEvents);
    }

    [Fact]
    public void Raising_a_null_event_throws()
    {
        var account = new Account(Guid.NewGuid());

        Assert.Throws<ArgumentNullException>(() => account.Record(null!));
    }
}
