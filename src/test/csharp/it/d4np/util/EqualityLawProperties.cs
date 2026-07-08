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
    public void ValueObject_differing_in_a_single_component_is_unequal(int amount, string? currency)
    {
        // Vary exactly one component at a time so a component-ignoring Equals (a GetEqualityComponents
        // that drops a field) is caught deterministically — two randomly generated instances almost never
        // collide on a single field, so an independent-pair test would miss the bug.
        var baseline = new Money(amount, currency);

        var differsInAmount = new Money(amount == int.MaxValue ? amount - 1 : amount + 1, currency);
        Assert.NotEqual(baseline, differsInAmount);
        Assert.True(baseline != differsInAmount);
        Assert.False(baseline == differsInAmount);

        var differsInCurrency = new Money(amount, currency == "x" ? "y" : "x");
        Assert.NotEqual(baseline, differsInCurrency);
        Assert.True(baseline != differsInCurrency);
        Assert.False(baseline == differsInCurrency);
    }

    [Property]
    public void ValueObject_equality_is_symmetric(int amount1, string? currency1, int amount2, string? currency2)
    {
        // Independent components so the pair is usually unequal; symmetry must hold either way.
        var x = new Money(amount1, currency1);
        var y = new Money(amount2, currency2);

        Assert.Equal(x.Equals(y), y.Equals(x));
        Assert.Equal(x == y, y == x);
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
    public void ValueObject_equal_instances_are_equal_and_share_a_hash_code(int amount, string? currency)
    {
        var x = new Money(amount, currency);
        var y = new Money(amount, currency);

        Assert.Equal(x, y);
        Assert.True(x == y);
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
