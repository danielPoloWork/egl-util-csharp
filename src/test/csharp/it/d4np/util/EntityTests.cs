using It.D4np.Util.Domain;

namespace It.D4np.Util.Tests;

public class EntityTests
{
    private sealed class Customer(Guid id) : Entity<Guid, Customer>(id);

    private sealed class Order(Guid id) : Entity<Guid, Order>(id);

    private sealed class Reference(string id) : Entity<string, Reference>(id);

    // A non-sealed entity plus a runtime subclass, standing in for an EF Core proxy
    // (CustomerProxy : Customer) — no EF dependency is needed to exercise canonical-type equality (#26).
    private class ProxiableCustomer(Guid id) : Entity<Guid, ProxiableCustomer>(id);

    private sealed class CustomerProxy(Guid id) : ProxiableCustomer(id);

    [Fact]
    public void Entities_with_the_same_type_and_id_are_equal()
    {
        var id = Guid.NewGuid();
        var a = new Customer(id);
        var b = new Customer(id);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        var a = new Customer(Guid.NewGuid());
        var b = new Customer(Guid.NewGuid());

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void Entities_of_different_types_with_the_same_id_are_not_equal()
    {
        var id = Guid.NewGuid();
        var customer = new Customer(id);
        var order = new Order(id);

        // Distinct canonical types compare unequal despite the shared Id. They have no common Entity base
        // to compare with `==`, so this goes through object equality.
        Assert.False(customer.Equals((object)order));
        Assert.False(order.Equals((object)customer));
        Assert.NotEqual<object>(customer, order);
    }

    [Fact]
    public void A_proxy_subclass_and_a_plain_instance_of_the_same_identity_are_equal()
    {
        // #26: an EF proxy is a runtime subclass; canonical-type equality makes it equal to a
        // plainly-constructed instance of the same identity — symmetrically, transitively, same hash.
        var id = Guid.NewGuid();
        var plain = new ProxiableCustomer(id);
        var proxy = new CustomerProxy(id);
        var plain2 = new ProxiableCustomer(id);

        Assert.Equal(plain, proxy);
        Assert.Equal(proxy, plain);                             // symmetric
        Assert.True(plain == proxy);
        Assert.Equal(plain.GetHashCode(), proxy.GetHashCode()); // equal instances share a hash
        Assert.Equal(proxy, plain2);                            // transitive: plain == proxy == plain2 ...
        Assert.Equal(plain, plain2);                            // ... therefore plain == plain2
    }

    [Fact]
    public void Transient_entities_with_a_default_id_compare_by_reference()
    {
        // #25: two distinct unsaved entities (default Id) are unequal and keep distinct hash codes, so they
        // do not collapse into one slot in a HashSet; each is still equal to itself.
        var a = new Customer(Guid.Empty);
        var b = new Customer(Guid.Empty);

        Assert.NotEqual(a, b);
        Assert.True(a != b);
        Assert.True(a.Equals(a)); // reflexive
        Assert.Equal(2, new HashSet<Customer> { a, b }.Count); // both retained, not merged
    }

    [Fact]
    public void Equality_handles_null_and_unrelated_types()
    {
        var a = new Customer(Guid.NewGuid());

        Assert.False(a.Equals(null));
        Assert.False(a.Equals("not an entity"));
        Assert.False(a == null);
        Assert.False(null == a);
        Assert.True((Customer?)null == (Customer?)null);
    }

    [Fact]
    public void Constructor_rejects_a_null_id()
    {
        Assert.Throws<ArgumentNullException>(() => new Reference(null!));
    }
}
