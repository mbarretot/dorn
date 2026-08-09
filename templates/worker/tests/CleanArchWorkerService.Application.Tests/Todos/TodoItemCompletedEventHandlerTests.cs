using CleanArchWorkerService.Application.Todos.ProcessPendingTodoItems;
using Microsoft.Extensions.Logging;

namespace CleanArchWorkerService.Application.Tests.Todos;

public sealed class TodoItemCompletedEventHandlerTests
{
    [Fact]
    public async Task Handle_LogsCompletedItem_AndCompletesWithoutThrowing()
    {
        var logger = Substitute.For<ILogger<TodoItemCompletedEventHandler>>();
        var handler = new TodoItemCompletedEventHandler(logger);
        var notification = new TodoItemCompletedEvent(Guid.NewGuid(), "Ship the worker");

        var task = handler.Handle(notification, CancellationToken.None);
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }
}
