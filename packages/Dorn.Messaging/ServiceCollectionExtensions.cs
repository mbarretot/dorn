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
                    // For an open-generic implementation (e.g. a pipeline behavior like
                    // ValidationBehavior<TRequest, TResponse>), `implementedInterface` is
                    // parameterized by the implementation's own generic parameters, not a
                    // true unbound generic type definition - registering it as-is makes the
                    // container treat it as a closed service mapped to an open implementation,
                    // which throws at ServiceProvider build time. Register against the true
                    // open generic definition instead so both sides stay unbound.
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
