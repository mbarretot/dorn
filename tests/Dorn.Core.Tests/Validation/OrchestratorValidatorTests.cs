using Dorn.Core.Validation;
using Xunit;

namespace Dorn.Core.Tests.Validation;

public class OrchestratorValidatorTests
{
    [Theory]
    [InlineData("aspire")]
    [InlineData("docker-compose")]
    [InlineData("none")]
    public void Validate_WithValidOrchestrator_ReturnsValid(string orchestrator)
    {
        var result = OrchestratorValidator.Validate(orchestrator);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithNullValue_ReturnsValid()
    {
        var result = OrchestratorValidator.Validate(null);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithInvalidValue_ReturnsInvalidMessageNamingAllThreeValues()
    {
        var result = OrchestratorValidator.Validate("invalid-value");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("'aspire'", result.ErrorMessage);
        Assert.Contains("'docker-compose'", result.ErrorMessage);
        Assert.Contains("'none'", result.ErrorMessage);
    }
}
