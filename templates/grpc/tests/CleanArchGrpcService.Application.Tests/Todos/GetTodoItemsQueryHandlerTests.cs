using CleanArchGrpcService.Application.Todos.GetTodoItems;

namespace CleanArchGrpcService.Application.Tests.Todos;

public sealed class GetTodoItemsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSeededItems_AsTodoItemDtoList()
    {
        var repository = Substitute.For<ITodoItemRepository>();
        repository
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TodoItem> { TodoItem.Create("first"), TodoItem.Create("second") });

        var handler = new GetTodoItemsQueryHandler(repository);

        var result = await handler.Handle(new GetTodoItemsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("first", result[0].Title);
        Assert.Equal("second", result[1].Title);
    }

    [Fact]
    public async Task Handle_WhenNoItemsSeeded_ReturnsEmptyList()
    {
        var repository = Substitute.For<ITodoItemRepository>();
        repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<TodoItem>());

        var handler = new GetTodoItemsQueryHandler(repository);

        var result = await handler.Handle(new GetTodoItemsQuery(), CancellationToken.None);

        Assert.Empty(result);
        await repository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }
}
