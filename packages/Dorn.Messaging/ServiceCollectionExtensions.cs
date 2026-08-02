using System.Reflection;
using Dorn.Messaging.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Dorn.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        Assembly assembly
    )
    {
        services.AddScoped<ISender, Mediator>();
        services.AddScoped<IPublisher, Mediator>();

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
                    || openGenericType == typeof(INotificationHandler<>)
                )
                {
                    // Open-generic implementations need the unbound service definition; registering their
                    // parameterized interface makes the container treat it as closed and fail at build time.
                    var serviceType = type.IsGenericTypeDefinition
                        ? openGenericType
                        : implementedInterface;

                    services.AddTransient(serviceType, type);
                }
            }
        }

        return services;
    }
}
