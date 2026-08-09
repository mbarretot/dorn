namespace CleanArchWorkerService.Functional.Tests;

/// <summary>
/// Boots a real <see cref="IHost"/> through the same <c>AddWorker</c> seam <c>Program.cs</c> uses,
/// against a temp SQLite file, so the Functional tier exercises the loop end-to-end (D7).
/// </summary>
public sealed class WorkerHostFixture : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"{Guid.NewGuid()}.db"
    );

    public IHost Host { get; private set; } = null!;

    public FakeTimeProvider TimeProvider { get; } = new();

    public async Task InitializeAsync()
    {
        var builder = HostFactory.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
            }
        );

        // Registered before AddWorker so its TryAddSingleton(TimeProvider.System) yields to this fake.
        builder.Services.AddSingleton<TimeProvider>(TimeProvider);

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddMediator(typeof(ProcessPendingTodoItemsCommand).Assembly);
        builder.Services.AddValidatorsFromAssembly(typeof(ProcessPendingTodoItemsCommand).Assembly);
        builder.Services.AddWorker(builder.Configuration);

        Host = builder.Build();

        await using var scope = Host.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Host.StopAsync();
        Host.Dispose();

        // Microsoft.Data.Sqlite pools native connections by file path, so disposing the host can leave
        // the database locked on Windows until SqliteConnection.ClearAllPools() is called.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
