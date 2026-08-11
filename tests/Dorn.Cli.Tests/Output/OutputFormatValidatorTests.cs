using Dorn.Cli.Output;
using Xunit;

namespace Dorn.Cli.Tests.Output;

public class OutputFormatValidatorTests
{
    [Fact]
    public void Validate_WithNullValue_ReturnsValidTable()
    {
        var result = OutputFormatValidator.Validate(null);

        Assert.True(result.IsValid);
        Assert.Equal(OutputFormat.Table, result.Format);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithEmptyValue_ReturnsValidTable()
    {
        var result = OutputFormatValidator.Validate(string.Empty);

        Assert.True(result.IsValid);
        Assert.Equal(OutputFormat.Table, result.Format);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithTable_ReturnsValidTable()
    {
        var result = OutputFormatValidator.Validate("table");

        Assert.True(result.IsValid);
        Assert.Equal(OutputFormat.Table, result.Format);
    }

    [Fact]
    public void Validate_WithJson_ReturnsValidJson()
    {
        var result = OutputFormatValidator.Validate("json");

        Assert.True(result.IsValid);
        Assert.Equal(OutputFormat.Json, result.Format);
    }

    [Fact]
    public void Validate_WithUppercaseJson_ReturnsValidJsonCaseInsensitively()
    {
        var result = OutputFormatValidator.Validate("JSON");

        Assert.True(result.IsValid);
        Assert.Equal(OutputFormat.Json, result.Format);
    }

    [Fact]
    public void Validate_WithUnknownValue_ReturnsInvalidWithMessage()
    {
        var result = OutputFormatValidator.Validate("xml");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("'table'", result.ErrorMessage);
        Assert.Contains("'json'", result.ErrorMessage);
    }
}
