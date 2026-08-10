using Microsoft.EntityFrameworkCore.Design;

namespace CleanArchWorkerService.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new ApplicationDbContext(options, NoOpPublisher.Instance);
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public static readonly NoOpPublisher Instance = new();

        public Task Publish(INotification notification, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
