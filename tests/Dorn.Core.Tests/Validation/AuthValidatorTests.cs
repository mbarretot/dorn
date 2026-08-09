using Dorn.Core.Validation;
using Xunit;

namespace Dorn.Core.Tests.Validation;

public class AuthValidatorTests
{
    [Theory]
    [InlineData("none")]
    [InlineData("custom")]
    [InlineData("azure-ad")]
    [InlineData("None")]
    [InlineData("CUSTOM")]
    [InlineData("Azure-Ad")]
    public void Validate_WithValidAuthMode_ReturnsValid(string auth)
    {
        var result = AuthValidator.Validate(auth);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithNullEmptyOrWhitespace_ReturnsValid(string? auth)
    {
        var result = AuthValidator.Validate(auth);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithBogusValue_ReturnsInvalidMessageNamingAllThreeValues()
    {
        var result = AuthValidator.Validate("bogus");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Unknown auth mode 'bogus'.", result.ErrorMessage);
        Assert.Contains("'none'", result.ErrorMessage);
        Assert.Contains("'custom'", result.ErrorMessage);
        Assert.Contains("'azure-ad'", result.ErrorMessage);
    }

    [Theory]
    [InlineData("jwt")]
    [InlineData("oauth")]
    [InlineData("basic")]
    public void Validate_WithOtherInvalidValue_ReturnsInvalid(string auth)
    {
        var result = AuthValidator.Validate(auth);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }
}
