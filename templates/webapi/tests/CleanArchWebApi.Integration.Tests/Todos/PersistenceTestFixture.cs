#if (UseSqlServer)
using Testcontainers.MsSql;
#endif

namespace CleanArchWebApi.Integration.Tests.Todos;

/// <summary>
/// Boots a real database — a SQL Server container via Testcontainers when
/// DatabaseProvider=sqlserver, a unique SQLite file otherwise — and applies the actual EF Core
/// migrations via Database.MigrateAsync(), proving they apply cleanly against the real provider.
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
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
#endif
    }
}
