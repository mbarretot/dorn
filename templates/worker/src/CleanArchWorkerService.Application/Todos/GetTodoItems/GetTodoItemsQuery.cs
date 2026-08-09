namespace CleanArchWorkerService.Application.Todos.GetTodoItems;

public sealed record GetTodoItemsQuery : IRequest<List<TodoItemDto>>;
