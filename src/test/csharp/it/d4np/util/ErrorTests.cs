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

    [Fact]
    public void Constructor_rejects_a_null_code()
    {
        // #76: back the non-nullable Code annotation with a runtime guard, so a null from outside NRT
        // enforcement (a #nullable disable caller, an explicit null!, or System.Text.Json materializing
        // the record from a payload with a missing Code) fails at construction, not far downstream.
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(() => new Error(null!, "message"));
        Assert.Equal("Code", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_null_message()
    {
        // #76: same guard for Message.
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(() => new Error("Some.Code", null!));
        Assert.Equal("Message", exception.ParamName);
    }
}
