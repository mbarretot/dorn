namespace CleanArchGrpcService.Infrastructure.Repositories.EfCore;

public class TodoItemRepository : ITodoItemRepository
{
    private readonly ApplicationDbContext _context;

    public TodoItemRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TodoItem todoItem, CancellationToken cancellationToken = default)
    {
        await _context.Items.AddAsync(todoItem, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TodoItem>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Items.ToListAsync(cancellationToken);
    }
}
