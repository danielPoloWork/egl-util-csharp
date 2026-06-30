using FsCheck.Xunit;

using It.D4np.Util.Domain;

namespace It.D4np.Util.Tests;

/// <summary>
/// Randomized property tests asserting that the equality of <see cref="ValueObject"/> and
/// <see cref="Entity{TId}"/> obeys the equivalence-relation laws (reflexive, symmetric,
/// transitive) and the equal-implies-equal-hash contract, across many generated inputs.
/// </summary>
public class EqualityLawProperties
{
    private sealed class Money(int amount, string? currency) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return amount;
            yield return currency;
        }
    }

    private sealed class Customer(int id) : Entity<int>(id);

    [Property]
    public void ValueObject_equality_is_reflexive(int amount, string? currency)
    {
        var x = new Money(amount, currency);

        Assert.True(x.Equals(x));
    }

    [Property]
    public void ValueObject_equality_is_symmetric(int amount, string? currency)
    {
        var x = new Money(amount, currency);
        var y = new Money(amount, currency);

        Assert.Equal(x.Equals(y), y.Equals(x));
        Assert.True(x == y);
    }

    [Property]
    public void ValueObject_equality_is_transitive(int amount, string? currency)
    {
        var x = new Money(amount, currency);
        var y = new Money(amount, currency);
        var z = new Money(amount, currency);

        Assert.True(x.Equals(y) && y.Equals(z) && x.Equals(z));
    }

    [Property]
    public void ValueObject_equal_instances_share_a_hash_code(int amount, string? currency)
    {
        var x = new Money(amount, currency);
        var y = new Money(amount, currency);

        Assert.Equal(x.GetHashCode(), y.GetHashCode());
    }

    [Property]
    public void Entity_equality_depends_only_on_identity(int id, int otherId)
    {
        var a = new Customer(id);
        var b = new Customer(id);

        Assert.Equal(a, b);

        if (id != otherId)
        {
            Assert.NotEqual(a, new Customer(otherId));
        }
    }
}
