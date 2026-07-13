namespace Dorn.Messaging.Contracts;

public interface IPublisher
{
    Task Publish(INotification notification, CancellationToken ct = default);
}
