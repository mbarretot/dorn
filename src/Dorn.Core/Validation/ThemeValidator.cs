namespace Dorn.Core.Validation;

public sealed record ThemeValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ThemeValidationResult Valid { get; } = new(true, null);

    public static ThemeValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}

public static class ThemeValidator
{
    public static readonly IReadOnlyList<string> ValidThemes =
    [
        "slate",
        "rose",
        "neutral",
        "linear",
        "primer",
        "lightning",
    ];

    public static ThemeValidationResult Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ThemeValidationResult.Valid;
        }

        if (!ValidThemes.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            var quotedThemes = string.Join(", ", ValidThemes.Select(theme => $"'{theme}'"));
            return ThemeValidationResult.Invalid(
                $"Unknown theme '{value}'. Valid values are {quotedThemes}."
            );
        }

        return ThemeValidationResult.Valid;
    }
}
