namespace CleanArchWorkerService.Integration.Tests.Todos;

/// <summary>
/// Boots a temporary SQLite database and applies EF Core migrations so the round-trip
/// test exercises the real provider against the generated migration files.
/// </summary>
public sealed class PersistenceTestFixture : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"{Guid.NewGuid()}.db"
    );

    public ApplicationDbContext DbContext { get; private set; } = null!;

    public string ConnectionString => $"Data Source={_databasePath}";

    public async Task InitializeAsync()
    {
        DbContext = CreateContext(Substitute.For<IPublisher>());
        await DbContext.Database.MigrateAsync();
    }

    public ApplicationDbContext CreateContext(IPublisher publisher)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(ConnectionString)
            .Options;

        return new ApplicationDbContext(options, publisher);
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();

        // Microsoft.Data.Sqlite pools native connections by file path, so disposing DbContext can leave
        // the database locked on Windows until SqliteConnection.ClearAllPools() is called.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
