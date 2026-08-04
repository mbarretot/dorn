namespace CleanArchGrpcService.Application.Todos.GetTodoItems;

public sealed record GetTodoItemsQuery : IRequest<List<TodoItemDto>>;
