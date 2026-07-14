# 0005. EF Core + SQLite as Default Persistence

## Status

Accepted

## Context

The `webapi` template's Infrastructure layer needs a persistence mechanism, and a Clean
Architecture template needs to decide both an ORM/data-access approach and a default
database provider. The Application layer must not depend on the concrete data-access
technology directly — it depends only on an abstraction it owns, which Infrastructure
implements — which constrains the choice less than it might seem, but the default
provider still matters because it determines whether a freshly generated project can run
immediately with zero external setup, or requires the contributor to install and
configure a database server before `dotnet run` produces anything usable.

EF Core is the de facto standard ORM in Clean Architecture .NET templates and the broader
.NET ecosystem: migrations, change tracking, and LINQ-based querying out of the box, and
the largest base of contributor familiarity of any .NET data-access library. Among EF
Core providers, SQLite requires no server process and no connection configuration beyond
a file path — `dotnet build && dotnet run` against a freshly generated project produces a
working, queryable database with no additional installation. SQL Server or PostgreSQL
would require the contributor to have a server running (locally, in a container, or
remote) before the generated project does anything useful, which is a poor default for a
scaffolding tool whose whole purpose is "get contributors to a running starting point
fast."

## Decision

The `webapi` template's Application layer defines `IApplicationDbContext` (in
`Application/Common/Persistence/`) as the persistence port — currently exposing a
`DbSet<TodoItem> Items` and `SaveChangesAsync`. Infrastructure implements it with a plain
EF Core `DbContext` (`Infrastructure/Persistence/ApplicationDbContext`), registered in
`AddInfrastructure(this IServiceCollection, IConfiguration)` via
`services.AddDbContext<ApplicationDbContext>(options =>
options.UseSqlite(configuration.GetConnectionString("Default")))`, with
`IApplicationDbContext` bound to the same registered instance. The default connection
string in `appsettings.json` (`"Default": "Data Source=app.db"`) points at a local SQLite
file, requiring no server setup.

`docs/templates/webapi.md` documents the manual steps to swap to PostgreSQL (replace the
EF Core provider package, change `UseSqlite(...)`/`UseSqlServer(...)` to `UseNpgsql(...)`
in `AddInfrastructure`, update the connection string, and regenerate migrations, since EF
Core migrations are provider-specific). SQL Server is no longer a manual swap — see
`docs/adr/0012-database-provider-selection.md` for the first-class `--database sqlserver`
choice added later.

The template ships a real `InitialCreate` EF Core migration
(`Infrastructure/Persistence/Migrations/`), and `WebApi/Program.cs` calls
`dbContext.Database.MigrateAsync()` against a scoped `ApplicationDbContext` on startup, so
the schema is created automatically the first time a generated project runs — this was
discovered to be missing during manual end-to-end verification (a generated project built
and ran, but every endpoint returned HTTP 500 with `SQLite Error 1: 'no such table:
Items'` until the migration and startup call were added) and is now confirmed working by
generating a project and exercising `POST`/`GET /api/todos` for real.

## Consequences

- A freshly generated `webapi` project builds and runs immediately with a working,
  migrated database, no external services and no manual `dotnet ef database update` step
  required — the fastest possible path from `dorn new webapi MyApp` to a runnable API.
- The Application layer stays provider-agnostic: it depends only on
  `IApplicationDbContext`, never on `Microsoft.EntityFrameworkCore.Sqlite` or any
  concrete `DbContext` type, so swapping providers is confined to the Infrastructure
  layer and configuration.
- SQLite is not a production-appropriate choice for every deployment target (limited
  concurrent-write support, no built-in replication) — this is explicitly a default for
  local development and getting started, not a recommendation to run SQLite in
  production. `docs/templates/webapi.md` documents the swap path for that reason.
- A known, accepted, non-blocking transitive vulnerability exists in the SQLite
  provider's native dependency: `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (pulled in by
  `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9) is flagged by GHSA-2m69-gcr7-jv3q
  (bundled SQLite older than 3.50.2), with no patched `SQLitePCLRaw` version published
  upstream yet (`first_patched_version` is `null`). This is tracked as a known,
  documented issue via a comment in `templates/webapi/Directory.Packages.props`; CI is
  not expected to fail on it, since there is currently no fix to apply.
