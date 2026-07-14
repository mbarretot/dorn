namespace Dorn.Core.Validation;

public sealed record OrchestratorValidationResult(bool IsValid, string? ErrorMessage)
{
    public static OrchestratorValidationResult Valid { get; } = new(true, null);

    public static OrchestratorValidationResult Invalid(string errorMessage) =>
        new(false, errorMessage);
}

/// <summary>
/// Validates the optional <c>--orchestrator</c> value passed to <c>dorn new webapi</c>.
/// </summary>
public static class OrchestratorValidator
{
    private static readonly HashSet<string> ValidOrchestrators = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "aspire",
        "docker-compose",
    };

    public static OrchestratorValidationResult Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OrchestratorValidationResult.Valid;
        }

        if (!ValidOrchestrators.Contains(value))
        {
            return OrchestratorValidationResult.Invalid(
                $"Unknown orchestrator '{value}'. Valid values are 'aspire', 'docker-compose'."
            );
        }

        return OrchestratorValidationResult.Valid;
    }
}
