namespace CleanArchWorkerService.Domain.Events;

public sealed record TodoItemCompletedEvent(Guid TodoItemId, string Title) : INotification;
