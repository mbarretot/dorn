namespace Dorn.Core.Validation;

public sealed record OrmValidationResult(bool IsValid, string? ErrorMessage)
{
    public static OrmValidationResult Valid { get; } = new(true, null);

    public static OrmValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}

public static class OrmValidator
{
    public static readonly IReadOnlyList<string> ValidOrms = ["efcore", "dapper"];

    public static OrmValidationResult Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OrmValidationResult.Valid;
        }

        if (!ValidOrms.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return OrmValidationResult.Invalid(
                $"Unknown ORM '{value}'. Valid values are 'efcore', 'dapper'."
            );
        }

        return OrmValidationResult.Valid;
    }
}
