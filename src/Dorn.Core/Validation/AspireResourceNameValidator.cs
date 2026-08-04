using System.Text.RegularExpressions;

namespace Dorn.Core.Validation;

public sealed record AspireResourceNameValidationResult(bool IsValid, string? ErrorMessage)
{
    public static AspireResourceNameValidationResult Valid { get; } = new(true, null);

    public static AspireResourceNameValidationResult Invalid(string errorMessage) =>
        new(false, errorMessage);
}

/// <summary>
/// Aspire resource names (ASPIRE006) allow only ASCII letters, digits, and hyphens —
/// stricter than <see cref="ProjectNameValidator"/>, so this only gates Aspire-hosted
/// database providers (any provider other than the zero-Docker `sqlite` default).
/// </summary>
public static partial class AspireResourceNameValidator
{
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9-]*$")]
    private static partial Regex AspireResourceNamePattern();

    public static AspireResourceNameValidationResult Validate(string name, string databaseProvider)
    {
        if (!AspireResourceNamePattern().IsMatch(name))
        {
            return AspireResourceNameValidationResult.Invalid(
                $"Project name '{name}' is not valid for '--database {databaseProvider}': the name is used as an "
                    + "Aspire resource name, which must contain only ASCII letters, digits, and hyphens. "
                    + "Choose a different name, or generate with '--database sqlite' instead."
            );
        }

        return AspireResourceNameValidationResult.Valid;
    }
}
