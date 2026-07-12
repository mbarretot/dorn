using Dorn.Core.Validation;
using Xunit;

namespace Dorn.Core.Tests.Validation;

public class ProjectNameValidatorTests
{
    [Theory]
    [InlineData("MyApp")]
    [InlineData("my_app")]
    [InlineData("_App")]
    [InlineData("App123")]
    [InlineData("A")]
    public void Validate_WithValidName_ReturnsValid(string name)
    {
        var result = ProjectNameValidator.Validate(name);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithNullName_ReturnsInvalid()
    {
        var result = ProjectNameValidator.Validate(null);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrWhitespaceName_ReturnsInvalid(string name)
    {
        var result = ProjectNameValidator.Validate(name);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("123App")]
    [InlineData("9")]
    public void Validate_WithNameStartingWithDigit_ReturnsInvalid(string name)
    {
        var result = ProjectNameValidator.Validate(name);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("-App")]
    [InlineData("!App")]
    public void Validate_WithNameNotStartingWithLetterOrUnderscore_ReturnsInvalid(string name)
    {
        var result = ProjectNameValidator.Validate(name);

        Assert.False(result.IsValid);
    }

    // Path.GetInvalidFileNameChars() differs per OS (Windows flags many more characters
    // than Unix), so this only asserts on '/', which both platforms reject.
    [Theory]
    [InlineData("My/App")]
    [InlineData("path/to/App")]
    public void Validate_WithInvalidPathCharacters_ReturnsInvalid(string name)
    {
        var result = ProjectNameValidator.Validate(name);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("LPT1")]
    [InlineData("COM9")]
    public void Validate_WithReservedWindowsDeviceName_ReturnsInvalid(string name)
    {
        var result = ProjectNameValidator.Validate(name);

        Assert.False(result.IsValid);
    }
}
