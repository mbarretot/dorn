namespace Dorn.Core.Validation;

public sealed record OrmValidationResult(bool IsValid, string? ErrorMessage)
{
    public static OrmValidationResult Valid { get; } = new(true, null);

    public static OrmValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// Validates the optional <c>--orm</c> value passed to <c>dorn new webapi</c>.
/// </summary>
public static class OrmValidator
{
    private static readonly HashSet<string> ValidOrms = new(StringComparer.OrdinalIgnoreCase)
    {
        "efcore",
        "dapper",
    };

    public static OrmValidationResult Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OrmValidationResult.Valid;
        }

        if (!ValidOrms.Contains(value))
        {
            return OrmValidationResult.Invalid(
                $"Unknown ORM '{value}'. Valid values are 'efcore', 'dapper'."
            );
        }

        return OrmValidationResult.Valid;
    }
}
