using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;

namespace Dorn.Core.Templating;

/// <summary>
/// Builds an isolated <see cref="EngineEnvironmentSettings"/> for Dorn under
/// ~/.dorn/template-engine (not ~/.templateengine) so it does not pollute the user's global
/// <c>dotnet new</c> cache. Microsoft.TemplateEngine.Edge 10.0.301 (matches .NET 10 SDK)
/// dropped the <c>Bootstrapper</c> façade — entry points are EngineEnvironmentSettings,
/// Scanner, and TemplateCreator; all wiring is kept behind this class + the templating
/// classes so the public API surface can be narrowed later without rippling beyond Dorn.Core.
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
