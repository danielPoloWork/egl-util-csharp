using It.D4np.Util.Domain;
using It.D4np.Util.Infrastructure;

namespace It.D4np.Util.Tests;

public class DbContextExtensionsTests
{
    private sealed record ProductCreated(DateTime OccurredOnUtc) : IDomainEvent;

    /// <summary>Persists a product (and its category, so the relational FK holds) so a later save can remove it.</summary>
    private static Product SeedProduct(CatalogContext context)
    {
        var category = new Category(Guid.NewGuid(), "cat");
        var product = new Product(Guid.NewGuid(), "widget", 1, category.Id);
        context.Categories.Add(category);
        context.Products.Add(product);
        context.SaveChanges();
        return product;
    }

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

    [Fact]
    public void CaptureDomainEvents_snapshots_without_clearing()
    {
        using var catalog = Catalog.Create(useSqlite: false);
        var product = new Product(Guid.NewGuid(), "ddd", 50, Guid.NewGuid());
        product.RaiseTestEvent(new ProductCreated(DateTime.UnixEpoch));
        catalog.Context.Products.Add(product);

        DomainEventSnapshot snapshot = catalog.Context.CaptureDomainEvents();

        Assert.Single(snapshot.Events);
        Assert.Single(product.DomainEvents); // capture does not clear
    }

    [Fact]
    public async Task Snapshot_of_no_events_dispatches_nothing()
    {
        using var catalog = Catalog.Create(useSqlite: false);
        catalog.Context.Products.Add(new Product(Guid.NewGuid(), "quiet", 1, Guid.NewGuid()));

        DomainEventSnapshot snapshot = catalog.Context.CaptureDomainEvents();

        Assert.Empty(snapshot.Events);
        var published = 0;
        await snapshot.DispatchDomainEventsAsync((_, _) =>
        {
            published++;
            return Task.CompletedTask;
        });
        Assert.Equal(0, published); // dispatch is a no-op when nothing was captured
    }

    [Fact]
    public async Task Snapshot_dispatch_publishes_in_order_then_clears_on_success()
    {
        using var catalog = Catalog.Create(useSqlite: false);
        var product = new Product(Guid.NewGuid(), "x", 1, Guid.NewGuid());
        var first = new ProductCreated(DateTime.UnixEpoch);
        var second = new ProductCreated(DateTime.UnixEpoch.AddDays(1));
        product.RaiseTestEvent(first);
        product.RaiseTestEvent(second);
        catalog.Context.Products.Add(product);

        DomainEventSnapshot snapshot = catalog.Context.CaptureDomainEvents();
        var published = new List<IDomainEvent>();
        await snapshot.DispatchDomainEventsAsync((domainEvent, _) =>
        {
            published.Add(domainEvent);
            return Task.CompletedTask;
        });

        Assert.Equal(new IDomainEvent[] { first, second }, published);
        Assert.Empty(product.DomainEvents); // cleared on success
        Assert.Equal(2, snapshot.Events.Count); // Events reflects the original capture, stable after dispatch
    }

    // The #21 bug: an aggregate removed by the save detaches from the change tracker, so the
    // change-tracker overload can no longer see its events. This pins that documented limitation.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Change_tracker_dispatch_drops_events_for_an_aggregate_removed_by_the_save(bool useSqlite)
    {
        using var catalog = Catalog.Create(useSqlite);
        Product product = SeedProduct(catalog.Context);

        product.RaiseTestEvent(new ProductCreated(DateTime.UnixEpoch));
        catalog.Context.Products.Remove(product);
        await catalog.Context.SaveChangesAsync(); // Deleted entity detaches from the change tracker

        var published = new List<IDomainEvent>();
        await catalog.Context.DispatchDomainEventsAsync((domainEvent, _) =>
        {
            published.Add(domainEvent);
            return Task.CompletedTask;
        });

        Assert.Empty(published); // the removed aggregate is gone from the tracker — its event is lost
        Assert.Single(product.DomainEvents); // still buffered on the (now-detached) object, never dispatched
    }

    // The #21 fix: capturing before the save preserves the removed aggregate's events for dispatch.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Snapshot_delivers_events_for_an_aggregate_removed_by_the_save(bool useSqlite)
    {
        using var catalog = Catalog.Create(useSqlite);
        Product product = SeedProduct(catalog.Context);

        var cancelled = new ProductCreated(DateTime.UnixEpoch);
        product.RaiseTestEvent(cancelled);
        catalog.Context.Products.Remove(product);

        // Capture BEFORE the save, while the aggregate is still tracked.
        DomainEventSnapshot snapshot = catalog.Context.CaptureDomainEvents();
        await catalog.Context.SaveChangesAsync();

        var published = new List<IDomainEvent>();
        await snapshot.DispatchDomainEventsAsync((domainEvent, _) =>
        {
            published.Add(domainEvent);
            return Task.CompletedTask;
        });

        Assert.Equal(new IDomainEvent[] { cancelled }, published); // delivered despite the delete
        Assert.Empty(product.DomainEvents); // cleared on success
    }

    [Fact]
    public async Task Snapshot_dispatch_redrives_only_undelivered_aggregates_after_a_failure()
    {
        using var catalog = Catalog.Create(useSqlite: false);
        var first = new Product(Guid.NewGuid(), "a", 1, Guid.NewGuid());
        var second = new Product(Guid.NewGuid(), "b", 1, Guid.NewGuid());
        var firstEvent = new ProductCreated(DateTime.UnixEpoch);
        var secondEvent = new ProductCreated(DateTime.UnixEpoch.AddDays(1));
        first.RaiseTestEvent(firstEvent);
        second.RaiseTestEvent(secondEvent);
        catalog.Context.Products.AddRange(first, second);

        DomainEventSnapshot snapshot = catalog.Context.CaptureDomainEvents();

        var published = new List<IDomainEvent>();
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            snapshot.DispatchDomainEventsAsync((domainEvent, _) =>
            {
                calls++;
                if (calls == 2)
                {
                    throw new InvalidOperationException("publish failed"); // fail on the second aggregate
                }

                published.Add(domainEvent);
                return Task.CompletedTask;
            }));

        Assert.Single(published); // exactly one aggregate delivered-and-cleared before the failure

        // Re-dispatching the same snapshot re-drives only the still-undelivered aggregate.
        await snapshot.DispatchDomainEventsAsync((domainEvent, _) =>
        {
            published.Add(domainEvent);
            return Task.CompletedTask;
        });

        Assert.Equal(2, published.Count); // no already-delivered event is re-published
        Assert.Contains(firstEvent, published);
        Assert.Contains(secondEvent, published);
        Assert.Empty(first.DomainEvents);
        Assert.Empty(second.DomainEvents);
    }
}
