using CleanArchWebApi.Application.Common.Persistence;
using CleanArchWebApi.Domain.Common.Interfaces;
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
#if (UseEfCore)
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

        services.AddScoped<ITodoItemRepository, Repositories.EfCore.TodoItemRepository>();
#endif

#if (UseDapper)
        services.AddScoped<Repositories.Dapper.DapperContext>();

        services.AddScoped<ITodoItemRepository, Repositories.Dapper.TodoItemRepository>();
#endif

        return services;
    }
}
