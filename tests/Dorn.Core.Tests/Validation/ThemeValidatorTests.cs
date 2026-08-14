using Dorn.Core.Validation;
using Xunit;

namespace Dorn.Core.Tests.Validation;

public class ThemeValidatorTests
{
    [Theory]
    [InlineData("slate")]
    [InlineData("rose")]
    [InlineData("SLATE")]
    public void Validate_WithAllowedTheme_ReturnsValid(string theme)
    {
        var result = ThemeValidator.Validate(theme);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithNullValue_ReturnsValid()
    {
        var result = ThemeValidator.Validate(null);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithInvalidValue_ReturnsInvalidMessageNamingBothValues()
    {
        var result = ThemeValidator.Validate("not-a-real-theme");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("'slate'", result.ErrorMessage);
        Assert.Contains("'rose'", result.ErrorMessage);
    }
}
