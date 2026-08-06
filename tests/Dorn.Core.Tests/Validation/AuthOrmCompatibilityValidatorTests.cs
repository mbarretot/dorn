using Dorn.Core.Validation;
using Xunit;

namespace Dorn.Core.Tests.Validation;

public class AuthOrmCompatibilityValidatorTests
{
    [Theory]
    [InlineData("custom", "sqlite")]
    [InlineData("custom", "sqlserver")]
    [InlineData("custom", "postgres")]
    [InlineData("custom", "efcore")]
    [InlineData("none", "dapper")]
    [InlineData("none", "efcore")]
    [InlineData("azure-ad", "dapper")]
    [InlineData("azure-ad", "efcore")]
    [InlineData("azure-ad", "sqlite")]
    public void Validate_WithCompatibleCombination_ReturnsValid(string auth, string orm)
    {
        var result = AuthOrmCompatibilityValidator.Validate(auth, orm);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("custom", "dapper")]
    [InlineData("Custom", "Dapper")]
    [InlineData("CUSTOM", "DAPPER")]
    public void Validate_WithCustomAndDapper_ReturnsInvalid(string auth, string orm)
    {
        var result = AuthOrmCompatibilityValidator.Validate(auth, orm);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Auth='custom' requires Orm='efcore'", result.ErrorMessage);
    }
}
