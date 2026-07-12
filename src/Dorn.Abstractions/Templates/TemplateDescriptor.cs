namespace Dorn.Abstractions.Templates;

public sealed record TemplateDescriptor(
    string Identity,
    string ShortName,
    string Name,
    string? Description,
    IReadOnlyList<string> Classifications,
    string SourcePath
);
