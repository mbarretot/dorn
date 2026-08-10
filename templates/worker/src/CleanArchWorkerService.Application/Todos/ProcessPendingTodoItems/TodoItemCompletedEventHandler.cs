using CleanArchWorkerService.Domain.Events;
using Microsoft.Extensions.Logging;

namespace CleanArchWorkerService.Application.Todos.ProcessPendingTodoItems;

public sealed class TodoItemCompletedEventHandler : INotificationHandler<TodoItemCompletedEvent>
{
    private readonly ILogger<TodoItemCompletedEventHandler> _logger;

    public TodoItemCompletedEventHandler(ILogger<TodoItemCompletedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(TodoItemCompletedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "Todo item {TodoItemId} ({Title}) was completed.",
            notification.TodoItemId,
            notification.Title
        );

        return Task.CompletedTask;
    }
}
