using It.D4np.Util.Domain;

namespace It.D4np.Util.Tests;

public class ErrorTests
{
    [Fact]
    public void Errors_are_value_equal()
    {
        Assert.Equal(new Error("User.Email.Required", "Email is required."),
                     new Error("User.Email.Required", "Email is required."));
        Assert.NotEqual(new Error("A", "m"), new Error("B", "m"));
        Assert.NotEqual(new Error("A", "m"), new Error("A", "n"));
    }

    [Fact]
    public void None_is_the_empty_sentinel()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Message);
    }
}
