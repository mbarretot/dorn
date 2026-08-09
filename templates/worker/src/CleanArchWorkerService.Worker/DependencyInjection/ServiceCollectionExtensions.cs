using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CleanArchWorkerService.Worker.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorker(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<WorkerOptions>()
            .Bind(configuration.GetSection(WorkerOptions.SectionName))
            // PeriodicTimer throws ArgumentOutOfRangeException on a non-positive period deep inside
            // ExecuteAsync. Fail at startup instead, with a message that names the setting.
            .Validate(o => o.Interval > TimeSpan.Zero, "Worker:Interval must be greater than zero.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<TodoProcessingWorker>();
        return services;
    }
}
