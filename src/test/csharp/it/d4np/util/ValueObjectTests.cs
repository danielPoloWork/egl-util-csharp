using It.D4np.Util.Domain;

namespace It.D4np.Util.Tests;

public class ValueObjectTests
{
    private sealed class Money(decimal amount, string currency) : ValueObject
    {
        public decimal Amount { get; } = amount;

        public string Currency { get; } = currency;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Fact]
    public void Value_objects_with_equal_components_are_equal()
    {
        var a = new Money(10m, "EUR");
        var b = new Money(10m, "EUR");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Value_objects_differing_in_any_component_are_not_equal()
    {
        Assert.NotEqual(new Money(10m, "EUR"), new Money(10m, "USD"));
        Assert.NotEqual(new Money(10m, "EUR"), new Money(11m, "EUR"));
    }

    [Fact]
    public void Equality_handles_null_and_unrelated_types()
    {
        var a = new Money(1m, "EUR");

        Assert.False(a.Equals(null));
        Assert.False(a.Equals(42));
        Assert.False(a == null);
        Assert.False(null == a);
    }
}
