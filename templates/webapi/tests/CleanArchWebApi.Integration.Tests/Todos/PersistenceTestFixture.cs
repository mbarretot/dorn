#if (UseSqlServer)
using Testcontainers.MsSql;
#endif

namespace CleanArchWebApi.Integration.Tests.Todos;

/// <summary>
/// Boots the selected real provider (Testcontainers SQL Server or temp-file SQLite) and applies EF Core migrations.
/// </summary>
public sealed class PersistenceTestFixture : IAsyncLifetime
{
#if (UseSqlServer)
    // Same image tag as docker-compose.SqlServer.yml, kept in sync deliberately.
    private readonly MsSqlContainer _container = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-latest"
    ).Build();
#else
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"{Guid.NewGuid()}.db"
    );
#endif

    public ApplicationDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
#if (UseSqlServer)
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
#else
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;
#endif

        DbContext = new ApplicationDbContext(options, Substitute.For<IPublisher>());
        await DbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();

#if (UseSqlServer)
        await _container.DisposeAsync();
#else
        // Microsoft.Data.Sqlite pools native connections by file path, so disposing DbContext can leave
        // the database locked on Windows until SqliteConnection.ClearAllPools() is called.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
#endif
    }
}
