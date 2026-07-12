using Dorn.Abstractions.Templates;

namespace Dorn.Abstractions.Generation;

public interface IGenerationEngine
{
    Task<IReadOnlyList<TemplateDescriptor>> ListTemplatesAsync(CancellationToken ct = default);

    Task<GenerationResult> GenerateAsync(GenerationRequest request, CancellationToken ct = default);
}
