using Dorn.Abstractions.Generation;
using Dorn.Abstractions.Templates;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;

namespace Dorn.Core.Templating;

/// <summary>
/// Implements IGenerationEngine on top of Microsoft.TemplateEngine.Edge.Template.TemplateCreator.
/// </summary>
public sealed class TemplateEngineGenerationEngine : IGenerationEngine
{
    private readonly FileSystemTemplateCatalog _catalog;
    private readonly TemplateCreator _templateCreator;

    public TemplateEngineGenerationEngine(
        FileSystemTemplateCatalog catalog,
        IEngineEnvironmentSettings environmentSettings
    )
    {
        _catalog = catalog;
        _templateCreator = new TemplateCreator(environmentSettings);
    }

    public Task<IReadOnlyList<TemplateDescriptor>> ListTemplatesAsync(
        CancellationToken ct = default
    ) => _catalog.GetAvailableTemplatesAsync(ct);

    public async Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        CancellationToken ct = default
    )
    {
        var templateInfo = await _catalog
            .FindTemplateInfoByShortNameAsync(request.TemplateShortName, ct)
            .ConfigureAwait(false);

        if (templateInfo is null)
        {
            return new GenerationResult(
                Success: false,
                OutputDirectory: request.OutputDirectory,
                CreatedFiles: Array.Empty<string>(),
                Diagnostics:
                [
                    new GenerationDiagnostic(
                        GenerationDiagnosticSeverity.Error,
                        $"No template found with short name '{request.TemplateShortName}'."
                    ),
                ]
            );
        }

        // The embedded host's default destructive-change handling (ITemplateEngineHost.
        // OnPotentiallyDestructiveChangesDetected) is permissive regardless of
        // forceCreation, so TemplateCreator.InstantiateAsync alone will happily overwrite
        // an existing non-empty output directory even when Force is false. Enforce the
        // --force contract ourselves with an explicit pre-check instead.
        if (
            !request.Force
            && Directory.Exists(request.OutputDirectory)
            && Directory.EnumerateFileSystemEntries(request.OutputDirectory).Any()
        )
        {
            return new GenerationResult(
                Success: false,
                OutputDirectory: request.OutputDirectory,
                CreatedFiles: Array.Empty<string>(),
                Diagnostics:
                [
                    new GenerationDiagnostic(
                        GenerationDiagnosticSeverity.Error,
                        $"Output directory '{request.OutputDirectory}' already exists and is not empty. "
                            + "Use --force to overwrite."
                    ),
                ]
            );
        }

        var parameters =
            request.Parameters?.ToDictionary(kv => kv.Key, kv => (string?)kv.Value)
            ?? new Dictionary<string, string?>();

        ITemplateCreationResult result;
        try
        {
            result = await _templateCreator
                .InstantiateAsync(
                    templateInfo,
                    name: request.ProjectName,
                    fallbackName: request.ProjectName,
                    outputPath: request.OutputDirectory,
                    inputParameters: parameters,
                    forceCreation: request.Force,
                    cancellationToken: ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new GenerationResult(
                Success: false,
                OutputDirectory: request.OutputDirectory,
                CreatedFiles: Array.Empty<string>(),
                Diagnostics:
                [
                    new GenerationDiagnostic(GenerationDiagnosticSeverity.Error, ex.Message),
                ]
            );
        }

        var success = result.Status == CreationResultStatus.Success;

        var createdFiles =
            result
                .CreationEffects?.FileChanges.Select(fc =>
                    Path.Combine(request.OutputDirectory, fc.TargetRelativePath)
                )
                .ToList()
            ?? [];

        var diagnostics = new List<GenerationDiagnostic>();
        if (!success)
        {
            diagnostics.Add(
                new GenerationDiagnostic(
                    GenerationDiagnosticSeverity.Error,
                    string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? $"Template generation failed with status '{result.Status}'."
                        : result.ErrorMessage
                )
            );
        }

        return new GenerationResult(
            Success: success,
            OutputDirectory: request.OutputDirectory,
            CreatedFiles: createdFiles,
            Diagnostics: diagnostics
        );
    }
}
