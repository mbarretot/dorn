using CleanArchWorkerService.Domain.Entities;

namespace CleanArchWorkerService.Domain.Repositories;

public interface ITodoItemRepository
{
    Task AddAsync(TodoItem todoItem, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TodoItem>> GetPendingAsync(CancellationToken cancellationToken = default);
}
