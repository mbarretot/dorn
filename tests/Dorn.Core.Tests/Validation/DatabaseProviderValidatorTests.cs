using Dorn.Core.Validation;
using Xunit;

namespace Dorn.Core.Tests.Validation;

public class DatabaseProviderValidatorTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("sqlserver")]
    [InlineData("postgres")]
    public void Validate_WithValidProvider_ReturnsValid(string provider)
    {
        var result = DatabaseProviderValidator.Validate(provider);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithNullValue_ReturnsValid()
    {
        var result = DatabaseProviderValidator.Validate(null);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithUnknownProvider_ReturnsInvalidMessageNamingAllThreeValues()
    {
        var result = DatabaseProviderValidator.Validate("mysql");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("'sqlite'", result.ErrorMessage);
        Assert.Contains("'sqlserver'", result.ErrorMessage);
        Assert.Contains("'postgres'", result.ErrorMessage);
    }
}
