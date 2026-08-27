namespace Dorn.Core.Validation;

public sealed record AuthValidationResult(bool IsValid, string? ErrorMessage)
{
    public static AuthValidationResult Valid { get; } = new(true, null);

    public static AuthValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}

public static class AuthValidator
{
    public static readonly IReadOnlyList<string> ValidAuthModes = ["none", "custom", "azure-ad"];

    public static AuthValidationResult Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AuthValidationResult.Valid;
        }

        if (!ValidAuthModes.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return AuthValidationResult.Invalid(
                $"Unknown auth mode '{value}'. Valid values are 'none', 'custom', 'azure-ad'."
            );
        }

        return AuthValidationResult.Valid;
    }
}
