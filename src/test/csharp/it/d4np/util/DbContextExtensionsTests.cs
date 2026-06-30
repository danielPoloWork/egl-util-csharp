using It.D4np.Util.Domain;
using It.D4np.Util.Infrastructure;

namespace It.D4np.Util.Tests;

public class DbContextExtensionsTests
{
    private sealed record ProductCreated(DateTime OccurredOnUtc) : IDomainEvent;

    [Fact]
    public void PeekDomainEvents_collects_without_clearing()
    {
        using var catalog = Catalog.Create(useSqlite: false);
        var product = new Product(Guid.NewGuid(), "ddd", 50, Guid.NewGuid());
        product.RaiseTestEvent(new ProductCreated(DateTime.UnixEpoch));
        catalog.Context.Products.Add(product);

        IReadOnlyList<IDomainEvent> events = catalog.Context.PeekDomainEvents();

        Assert.Single(events);
        Assert.Single(product.DomainEvents); // peek does not clear
    }

    [Fact]
    public void PeekDomainEvents_is_empty_when_no_aggregate_has_events()
    {
        using var catalog = Catalog.Create(useSqlite: false);
        catalog.Context.Products.Add(new Product(Guid.NewGuid(), "quiet", 1, Guid.NewGuid()));

        Assert.Empty(catalog.Context.PeekDomainEvents());
    }

    [Fact]
    public void ClearDomainEvents_empties_every_tracked_aggregate()
    {
        using var catalog = Catalog.Create(useSqlite: false);
        var product = new Product(Guid.NewGuid(), "x", 1, Guid.NewGuid());
        product.RaiseTestEvent(new ProductCreated(DateTime.UnixEpoch));
        catalog.Context.Products.Add(product);

        catalog.Context.ClearDomainEvents();

        Assert.Empty(product.DomainEvents);
    }

    [Fact]
    public async Task DispatchDomainEventsAsync_publishes_in_order_then_clears_on_success()
    {
        using var catalog = Catalog.Create(useSqlite: false);
        var product = new Product(Guid.NewGuid(), "x", 1, Guid.NewGuid());
        var first = new ProductCreated(DateTime.UnixEpoch);
        var second = new ProductCreated(DateTime.UnixEpoch.AddDays(1));
        product.RaiseTestEvent(first);
        product.RaiseTestEvent(second);
        catalog.Context.Products.Add(product);

        var published = new List<IDomainEvent>();
        await catalog.Context.DispatchDomainEventsAsync((domainEvent, _) =>
        {
            published.Add(domainEvent);
            return Task.CompletedTask;
        });

        Assert.Equal(new IDomainEvent[] { first, second }, published);
        Assert.Empty(product.DomainEvents); // cleared on success
    }

    [Fact]
    public async Task DispatchDomainEventsAsync_keeps_events_buffered_when_a_publish_fails()
    {
        using var catalog = Catalog.Create(useSqlite: false);
        var product = new Product(Guid.NewGuid(), "x", 1, Guid.NewGuid());
        product.RaiseTestEvent(new ProductCreated(DateTime.UnixEpoch));
        catalog.Context.Products.Add(product);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => catalog.Context.DispatchDomainEventsAsync(
                (_, _) => throw new InvalidOperationException("publish failed")));

        // Not cleared: the undelivered event remains buffered for a retry (at-least-once).
        Assert.Single(product.DomainEvents);
    }
}
