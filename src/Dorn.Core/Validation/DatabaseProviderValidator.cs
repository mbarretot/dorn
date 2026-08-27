namespace Dorn.Core.Validation;

public sealed record DatabaseProviderValidationResult(bool IsValid, string? ErrorMessage)
{
    public static DatabaseProviderValidationResult Valid { get; } = new(true, null);

    public static DatabaseProviderValidationResult Invalid(string errorMessage) =>
        new(false, errorMessage);
}

public static class DatabaseProviderValidator
{
    public static readonly IReadOnlyList<string> ValidProviders =
    [
        "sqlite",
        "sqlserver",
        "postgres",
    ];

    public static DatabaseProviderValidationResult Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DatabaseProviderValidationResult.Valid;
        }

        if (!ValidProviders.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return DatabaseProviderValidationResult.Invalid(
                $"Unknown database provider '{value}'. Valid values are 'sqlite', 'sqlserver', 'postgres'."
            );
        }

        return DatabaseProviderValidationResult.Valid;
    }
}
