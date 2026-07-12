namespace Dorn.Core.Validation;

public sealed record ProjectNameValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ProjectNameValidationResult Valid { get; } = new(true, null);

    public static ProjectNameValidationResult Invalid(string errorMessage) =>
        new(false, errorMessage);
}

/// <summary>
/// Validates that a proposed project name is reasonable both as a filesystem directory
/// name and as the root of a generated C# identifier/namespace.
/// </summary>
public static class ProjectNameValidator
{
    private static readonly char[] InvalidPathChars = Path.GetInvalidFileNameChars();

    private static readonly HashSet<string> ReservedWindowsDeviceNames = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
    };

    public static ProjectNameValidationResult Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ProjectNameValidationResult.Invalid("Project name cannot be empty.");
        }

        var trimmed = name.Trim();

        if (trimmed.IndexOfAny(InvalidPathChars) >= 0)
        {
            return ProjectNameValidationResult.Invalid(
                $"Project name '{trimmed}' contains characters that are not valid in a file or directory name."
            );
        }

        if (char.IsDigit(trimmed[0]))
        {
            return ProjectNameValidationResult.Invalid(
                $"Project name '{trimmed}' cannot start with a digit."
            );
        }

        if (!(char.IsLetter(trimmed[0]) || trimmed[0] == '_'))
        {
            return ProjectNameValidationResult.Invalid(
                $"Project name '{trimmed}' must start with a letter or underscore."
            );
        }

        if (ReservedWindowsDeviceNames.Contains(trimmed))
        {
            return ProjectNameValidationResult.Invalid(
                $"Project name '{trimmed}' is a reserved system device name and cannot be used."
            );
        }

        return ProjectNameValidationResult.Valid;
    }
}
