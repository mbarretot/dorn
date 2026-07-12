namespace Dorn.Abstractions.Generation;

public sealed record GenerationResult(
    bool Success,
    string OutputDirectory,
    IReadOnlyList<string> CreatedFiles,
    IReadOnlyList<GenerationDiagnostic> Diagnostics
);
