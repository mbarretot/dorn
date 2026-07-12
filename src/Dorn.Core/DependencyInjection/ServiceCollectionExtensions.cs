using Dorn.Abstractions.Generation;
using Dorn.Abstractions.Templates;
using Dorn.Core.Templating;
using Microsoft.Extensions.DependencyInjection;

namespace Dorn.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Dorn's generation engine and template catalog. Everything here is a
    /// singleton: the Template Engine environment (host, settings, scanned template
    /// cache) is expensive to set up and safe to share for the lifetime of the process.
    /// </summary>
    public static IServiceCollection AddDornCore(this IServiceCollection services)
    {
        services.AddSingleton(_ => DornTemplateEngineHost.CreateEnvironmentSettings());
        services.AddSingleton<FileSystemTemplateCatalog>();
        services.AddSingleton<ITemplateCatalog>(sp =>
            sp.GetRequiredService<FileSystemTemplateCatalog>()
        );
        services.AddSingleton<IGenerationEngine, TemplateEngineGenerationEngine>();

        return services;
    }
}
