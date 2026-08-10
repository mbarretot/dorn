using CleanArchWorkerService.Application.Todos.CreateTodoItem;
using CleanArchWorkerService.Domain.Entities;
using CleanArchWorkerService.Domain.Repositories;

namespace CleanArchWorkerService.Application.Tests.Todos;

public sealed class CreateTodoItemCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsTodoItemToRepository_AndReturnsItsId()
    {
        var repository = Substitute.For<ITodoItemRepository>();
        var handler = new CreateTodoItemCommandHandler(repository);
        var command = new CreateTodoItemCommand("Write the Dorn scaffolding");

        var id = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);

        await repository
            .Received(1)
            .AddAsync(
                Arg.Is<TodoItem>(item => item.Title == "Write the Dorn scaffolding"),
                Arg.Any<CancellationToken>()
            );
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
