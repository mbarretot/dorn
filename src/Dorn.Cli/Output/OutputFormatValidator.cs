namespace Dorn.Cli.Output;

public sealed record OutputFormatValidationResult(
    bool IsValid,
    OutputFormat Format,
    string? ErrorMessage
)
{
    public static OutputFormatValidationResult Valid(OutputFormat format) =>
        new(true, format, null);

    public static OutputFormatValidationResult Invalid(string errorMessage) =>
        new(false, OutputFormat.Table, errorMessage);
}

public static class OutputFormatValidator
{
    public static OutputFormatValidationResult Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OutputFormatValidationResult.Valid(OutputFormat.Table);
        }

        if (string.Equals(value, "table", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormatValidationResult.Valid(OutputFormat.Table);
        }

        if (string.Equals(value, "json", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormatValidationResult.Valid(OutputFormat.Json);
        }

        return OutputFormatValidationResult.Invalid(
            $"Unknown format '{value}'. Valid values are 'table', 'json'."
        );
    }
}
