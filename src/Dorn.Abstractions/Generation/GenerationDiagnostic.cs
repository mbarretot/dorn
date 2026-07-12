namespace Dorn.Abstractions.Generation;

public enum GenerationDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record GenerationDiagnostic(GenerationDiagnosticSeverity Severity, string Message);
