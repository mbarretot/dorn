using Microsoft.EntityFrameworkCore.Design;

namespace CleanArchGrpcService.Infrastructure.Persistence;

/// <summary>
/// Used by the <c>dotnet ef</c> CLI at design time to discover the DbContext and a
/// connection string without needing the Web host (Grpc project) or
/// <c>appsettings.json</c>. The migrations folder is generated from this factory.
/// </summary>
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
