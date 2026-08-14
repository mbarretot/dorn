namespace Dorn.Core.Validation;

public sealed record ThemeValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ThemeValidationResult Valid { get; } = new(true, null);

    public static ThemeValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}

public static class ThemeValidator
{
    private static readonly HashSet<string> ValidThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "slate",
        "rose",
    };

    public static ThemeValidationResult Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ThemeValidationResult.Valid;
        }

        if (!ValidThemes.Contains(value))
        {
            return ThemeValidationResult.Invalid(
                $"Unknown theme '{value}'. Valid values are 'slate', 'rose'."
            );
        }

        return ThemeValidationResult.Valid;
    }
}
