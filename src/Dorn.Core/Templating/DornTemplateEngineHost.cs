using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;

namespace Dorn.Core.Templating;

/// <summary>
/// Builds isolated <see cref="EngineEnvironmentSettings"/> for Dorn. Microsoft.TemplateEngine.Edge 10.0.301 removed <c>Bootstrapper</c>, so the engine is wired directly with isolated settings.
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
