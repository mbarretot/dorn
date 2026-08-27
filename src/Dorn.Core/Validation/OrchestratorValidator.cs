namespace Dorn.Core.Validation;

public sealed record OrchestratorValidationResult(bool IsValid, string? ErrorMessage)
{
    public static OrchestratorValidationResult Valid { get; } = new(true, null);

    public static OrchestratorValidationResult Invalid(string errorMessage) =>
        new(false, errorMessage);
}

public static class OrchestratorValidator
{
    public static readonly IReadOnlyList<string> ValidOrchestrators =
    [
        "aspire",
        "docker-compose",
        "none",
    ];

    public static OrchestratorValidationResult Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OrchestratorValidationResult.Valid;
        }

        if (!ValidOrchestrators.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return OrchestratorValidationResult.Invalid(
                $"Unknown orchestrator '{value}'. Valid values are 'aspire', 'docker-compose', 'none'."
            );
        }

        return OrchestratorValidationResult.Valid;
    }
}
