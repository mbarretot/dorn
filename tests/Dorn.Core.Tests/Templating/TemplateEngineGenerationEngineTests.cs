using Dorn.Abstractions.Generation;
using Dorn.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dorn.Core.Tests.Templating;

/// <summary>
/// Exercises the real Template Engine wiring (DornTemplateEngineHost + FileSystemTemplateCatalog +
/// TemplateEngineGenerationEngine) against a tiny throwaway fixture template
/// (Fixtures/minimal-fixture-template), not the full webapi template — that full-template
/// generate+build coverage lives in Templates.Tests instead, kept slow and separate.
/// </summary>
public class TemplateEngineGenerationEngineTests : IDisposable
{
    private const string EnvironmentVariableName = "DORN_TEMPLATES_PATH";

    private readonly string? _originalEnvValue;
    private readonly string _outputDirectory;
    private readonly ServiceProvider _serviceProvider;

    public TemplateEngineGenerationEngineTests()
    {
        _originalEnvValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);

        // Fixtures/** is copied next to the test assembly (see Dorn.Core.Tests.csproj), so
        // the fixture template lives at <output>/Fixtures/minimal-fixture-template.
        var fixturesRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        Environment.SetEnvironmentVariable(EnvironmentVariableName, fixturesRoot);

        var services = new ServiceCollection();
        services.AddDornCore();
        _serviceProvider = services.BuildServiceProvider();

        _outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-fixture-{Guid.NewGuid():N}");
    }

    [Fact]
    public async Task GenerateAsync_WithKnownShortName_SucceedsAndSubstitutesContent()
    {
        var engine = _serviceProvider.GetRequiredService<IGenerationEngine>();
        var request = new GenerationRequest("dorn-fixture-minimal", "MyFixture", _outputDirectory);

        var result = await engine.GenerateAsync(request);

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.NotEmpty(result.CreatedFiles);

        var infoFile = Path.Combine(_outputDirectory, "Info.txt");
        Assert.True(File.Exists(infoFile));

        var content = await File.ReadAllTextAsync(infoFile);
        Assert.Contains("MyFixture", content);
        Assert.DoesNotContain("FixtureProject", content);
    }

    [Fact]
    public async Task GenerateAsync_WithUnknownShortName_ReturnsFailureWithDiagnostic()
    {
        var engine = _serviceProvider.GetRequiredService<IGenerationEngine>();
        var request = new GenerationRequest("dorn-does-not-exist", "MyFixture", _outputDirectory);

        var result = await engine.GenerateAsync(request);

        Assert.False(result.Success);
        Assert.Empty(result.CreatedFiles);
        Assert.Contains(result.Diagnostics, d => d.Severity == GenerationDiagnosticSeverity.Error);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        Environment.SetEnvironmentVariable(EnvironmentVariableName, _originalEnvValue);

        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }
}
