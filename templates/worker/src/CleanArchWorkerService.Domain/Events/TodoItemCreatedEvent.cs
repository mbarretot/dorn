namespace CleanArchWorkerService.Domain.Events;

public sealed record TodoItemCreatedEvent(Guid TodoItemId, string Title) : INotification;
