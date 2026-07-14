using CleanArchWebApi.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchWebApi.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<ApplicationDbContext>(options =>
#if (UseSqlServer)
            options.UseSqlServer(configuration.GetConnectionString("CleanArchWebApi"))
#else
            options.UseSqlite(configuration.GetConnectionString("Default"))
#endif
        );

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>()
        );

        return services;
    }
}
