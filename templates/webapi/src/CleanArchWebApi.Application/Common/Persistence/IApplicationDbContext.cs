namespace CleanArchWebApi.Application.Common.Persistence;

public interface IApplicationDbContext
{
    DbSet<TodoItem> Items { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
