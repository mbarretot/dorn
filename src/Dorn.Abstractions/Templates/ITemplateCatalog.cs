namespace Dorn.Abstractions.Templates;

public interface ITemplateCatalog
{
    Task<IReadOnlyList<TemplateDescriptor>> GetAvailableTemplatesAsync(
        CancellationToken ct = default
    );

    Task<TemplateDescriptor?> FindByShortNameAsync(
        string shortName,
        CancellationToken ct = default
    );
}
