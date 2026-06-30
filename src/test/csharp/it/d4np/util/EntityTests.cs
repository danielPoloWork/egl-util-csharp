using It.D4np.Util.Domain;

namespace It.D4np.Util.Tests;

public class EntityTests
{
    private sealed class Customer(Guid id) : Entity<Guid>(id);

    private sealed class Order(Guid id) : Entity<Guid>(id);

    private sealed class Reference(string id) : Entity<string>(id);

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
        Entity<Guid> a = new Customer(id);
        Entity<Guid> b = new Order(id);

        Assert.NotEqual(a, b);
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
