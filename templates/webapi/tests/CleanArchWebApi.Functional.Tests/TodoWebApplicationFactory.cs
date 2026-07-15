namespace CleanArchWebApi.Functional.Tests;

/// <summary>
/// Points ApplicationDbContext at a unique temp-file SQLite database instead of the raw
/// appsettings.json "Data Source=app.db" path, which would race across parallel test runs
/// (SQLite file locking is stricter on Windows). Always SQLite regardless of the generated
/// DatabaseProvider — this tier proves the HTTP pipeline, not provider fidelity (that's
/// CleanArchWebApi.Integration.Tests's job).
/// </summary>
public sealed class TodoWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"{Guid.NewGuid()}.db"
    );

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // AddDbContext appends config via Add, not TryAdd — removing only
            // DbContextOptions<T> leaves Program.cs's original provider registered alongside
            // this one, and EF Core throws seeing two providers. Remove both.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options
                    .UseSqlite($"Data Source={_databasePath}")
                    // In a --database sqlserver generation, the checked-in migrations were
                    // snapshotted against SQL Server, so EF's model differ flags a false
                    // "pending changes" warning when evaluated against SQLite. Documented
                    // suppression: https://aka.ms/efcore-docs-pending-changes.
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(RelationalEventId.PendingModelChangesWarning)
                    )
            );
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
