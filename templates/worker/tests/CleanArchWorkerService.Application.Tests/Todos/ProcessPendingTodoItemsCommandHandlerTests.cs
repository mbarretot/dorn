using CleanArchWorkerService.Application.Todos.ProcessPendingTodoItems;

namespace CleanArchWorkerService.Application.Tests.Todos;

public sealed class ProcessPendingTodoItemsCommandHandlerTests
{
    [Fact]
    public async Task Handle_MarksEveryPendingItem_SavesOnce_AndReturnsCount()
    {
        var repository = Substitute.For<ITodoItemRepository>();
        var pending = new List<TodoItem> { TodoItem.Create("first"), TodoItem.Create("second") };
        repository.GetPendingAsync(Arg.Any<CancellationToken>()).Returns(pending);
        var handler = new ProcessPendingTodoItemsCommandHandler(repository);

        var result = await handler.Handle(
            new ProcessPendingTodoItemsCommand(),
            CancellationToken.None
        );

        Assert.Equal(2, result);
        Assert.All(pending, item => Assert.True(item.IsComplete));
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoPendingItems_ReturnsZero_ButStillSaves()
    {
        var repository = Substitute.For<ITodoItemRepository>();
        repository.GetPendingAsync(Arg.Any<CancellationToken>()).Returns(new List<TodoItem>());
        var handler = new ProcessPendingTodoItemsCommandHandler(repository);

        var result = await handler.Handle(
            new ProcessPendingTodoItemsCommand(),
            CancellationToken.None
        );

        Assert.Equal(0, result);
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
