namespace Dorn.Core.Validation;

public sealed record DatabaseProviderValidationResult(bool IsValid, string? ErrorMessage)
{
    public static DatabaseProviderValidationResult Valid { get; } = new(true, null);

    public static DatabaseProviderValidationResult Invalid(string errorMessage) =>
        new(false, errorMessage);
}

public static class DatabaseProviderValidator
{
    private static readonly HashSet<string> ValidProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "sqlite",
        "sqlserver",
        "postgres",
    };

    public static DatabaseProviderValidationResult Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DatabaseProviderValidationResult.Valid;
        }

        if (!ValidProviders.Contains(value))
        {
            return DatabaseProviderValidationResult.Invalid(
                $"Unknown database provider '{value}'. Valid values are 'sqlite', 'sqlserver', 'postgres'."
            );
        }

        return DatabaseProviderValidationResult.Valid;
    }
}
