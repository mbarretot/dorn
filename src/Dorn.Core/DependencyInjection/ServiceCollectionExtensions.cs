using Dorn.Abstractions.Generation;
using Dorn.Abstractions.Templates;
using Dorn.Core.Templating;
using Microsoft.Extensions.DependencyInjection;

namespace Dorn.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the generation engine and template catalog as singletons because template scanning is expensive and safely shared.
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
