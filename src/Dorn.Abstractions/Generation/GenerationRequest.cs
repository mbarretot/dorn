namespace Dorn.Abstractions.Generation;

public sealed record GenerationRequest(
    string TemplateShortName,
    string ProjectName,
    string OutputDirectory,
    IReadOnlyDictionary<string, string>? Parameters = null,
    bool Force = false
);
