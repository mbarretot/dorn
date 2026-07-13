using Dorn.Messaging.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Dorn.Messaging;

public sealed class Mediator : ISender, IPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken ct = default
    )
    {
        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(
            requestType,
            typeof(TResponse)
        );
        var handlerHandleMethod =
            handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.Handle))
            ?? throw new InvalidOperationException(
                $"'{handlerType}' does not expose a Handle method."
            );

        var handler =
            _serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No handler registered for request type '{requestType.FullName}'."
            );

        RequestHandlerDelegate<TResponse> handlerDelegate = () =>
            (Task<TResponse>)handlerHandleMethod.Invoke(handler, [request, ct])!;

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(
            requestType,
            typeof(TResponse)
        );
        var behaviors = _serviceProvider
            .GetServices(behaviorType)
            .Cast<object>()
            .Reverse()
            .ToArray();

        if (behaviors.Length > 0)
        {
            var behaviorHandleMethod =
                behaviorType.GetMethod(
                    nameof(IPipelineBehavior<IRequest<TResponse>, TResponse>.Handle)
                )
                ?? throw new InvalidOperationException(
                    $"'{behaviorType}' does not expose a Handle method."
                );

            foreach (var behavior in behaviors)
            {
                var next = handlerDelegate;
                handlerDelegate = () =>
                    (Task<TResponse>)behaviorHandleMethod.Invoke(behavior, [request, next, ct])!;
            }
        }

        return handlerDelegate();
    }

    public async Task Publish(INotification notification, CancellationToken ct = default)
    {
        var notificationType = notification.GetType();
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);
        var handleMethod =
            handlerType.GetMethod(nameof(INotificationHandler<INotification>.Handle))
            ?? throw new InvalidOperationException(
                $"'{handlerType}' does not expose a Handle method."
            );

        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            await (Task)handleMethod.Invoke(handler, [notification, ct])!;
        }
    }
}
