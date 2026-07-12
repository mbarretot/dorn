using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;

namespace Dorn.Core.Templating;

/// <summary>
/// Builds an isolated Template Engine environment for Dorn.
///
/// Dorn embeds Microsoft.TemplateEngine.Edge directly instead of shelling out to
/// `dotnet new`, so it must not read from or write to the user's global `dotnet new`
/// settings/cache at ~/.templateengine. Doing so would (a) pollute the user's own
/// `dotnet new --list` output with Dorn's templates and (b) make Dorn's behavior depend
/// on unrelated global state. Instead every Dorn install gets its own settings location
/// under ~/.dorn/template-engine.
///
/// Note: this version of Microsoft.TemplateEngine.Edge (10.0.301, matching the .NET 10
/// SDK) does not expose the old `Bootstrapper` façade that older docs/samples reference.
/// The current entry points are `EngineEnvironmentSettings`, `Scanner`
/// (Microsoft.TemplateEngine.Edge.Settings) for discovery, and `TemplateCreator`
/// (Microsoft.TemplateEngine.Edge.Template) for instantiation. All of that wiring is kept
/// isolated behind this class and FileSystemTemplateCatalog/TemplateEngineGenerationEngine
/// so a future narrowing of the public API surface only touches Dorn.Core.
/// </summary>
public static class DornTemplateEngineHost
{
    private const string HostIdentifier = "dorn";
    private const string HostVersion = "1.0.0";

    public static IEngineEnvironmentSettings CreateEnvironmentSettings()
    {
        var builtIns = new List<(Type, IIdentifiedComponent)>();
        builtIns.AddRange(Components.AllComponents);
        builtIns.AddRange(
            Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components.AllComponents
        );

        var host = new DefaultTemplateEngineHost(HostIdentifier, HostVersion, builtIns: builtIns);

        return new EngineEnvironmentSettings(
            host,
            virtualizeSettings: false,
            settingsLocation: GetIsolatedSettingsLocation()
        );
    }

    private static string GetIsolatedSettingsLocation()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".dorn", "template-engine");
    }
}
