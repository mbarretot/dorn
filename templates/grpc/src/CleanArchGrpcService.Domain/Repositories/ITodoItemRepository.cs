using CleanArchGrpcService.Domain.Entities;

namespace CleanArchGrpcService.Domain.Repositories;

public interface ITodoItemRepository
{
    Task AddAsync(TodoItem todoItem, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default);
}
