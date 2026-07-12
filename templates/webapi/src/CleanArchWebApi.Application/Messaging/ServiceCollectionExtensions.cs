using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchWebApi.Application.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        Assembly assembly
    )
    {
        services.AddScoped<ISender, Mediator>();

        var candidateTypes = assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var type in candidateTypes)
        {
            foreach (var implementedInterface in type.GetInterfaces())
            {
                if (!implementedInterface.IsGenericType)
                {
                    continue;
                }

                var openGenericType = implementedInterface.GetGenericTypeDefinition();

                if (
                    openGenericType == typeof(IRequestHandler<,>)
                    || openGenericType == typeof(IPipelineBehavior<,>)
                )
                {
                    services.AddTransient(implementedInterface, type);
                }
            }
        }

        return services;
    }
}
