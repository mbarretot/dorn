namespace CleanArchGrpcService.Application.Todos.CreateTodoItem;

public sealed record CreateTodoItemCommand(string Title) : IRequest<Guid>;
