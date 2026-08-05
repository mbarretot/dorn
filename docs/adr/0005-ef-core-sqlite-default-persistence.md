# 0005. EF Core + SQLite as Default Persistence

## Status

Accepted

## Context

The `webapi` template's Infrastructure layer needs a persistence mechanism. The
Application layer depends only on a persistence abstraction it owns, but the default
provider still determines whether a freshly generated project runs immediately with zero
external setup, or needs a database server first.

EF Core is the de facto standard ORM in Clean Architecture .NET templates, shipping
migrations, change tracking, and LINQ querying out of the box, with the largest
contributor familiarity of any .NET data-access library. SQLite needs no server process
and no connection configuration beyond a file path: `dotnet build && dotnet run` produces
a working, queryable database immediately. SQL Server or PostgreSQL would require a
running server first, a poor default for a scaffolding tool meant to get contributors to
a running start fast.

## Decision

- The `webapi` template's Application layer defines `IApplicationDbContext` (in
  `Application/Common/Persistence/`) as the persistence port, exposing `DbSet<TodoItem>
  Items` and `SaveChangesAsync`.
- Infrastructure implements it with a plain EF Core `DbContext`
  (`Infrastructure/Persistence/ApplicationDbContext`), registered via
  `services.AddDbContext<ApplicationDbContext>(options =>
  options.UseSqlite(configuration.GetConnectionString("Default")))`, with
  `IApplicationDbContext` bound to the same instance.
- The default connection string in `appsettings.json`
  (`"Default": "Data Source=app.db"`) points at a local SQLite file.

`docs/templates/webapi.md` documents the manual steps to swap to PostgreSQL. SQL Server
is no longer a manual swap: see `docs/adr/0011-database-provider-selection.md` for the
first-class `--database sqlserver` choice added later.

The template ships a real `InitialCreate` EF Core migration
(`Infrastructure/Persistence/Migrations/`), and `WebApi/Program.cs` calls
`dbContext.Database.MigrateAsync()` on startup, so the schema is created automatically.
This was found missing during manual verification (every endpoint returned HTTP 500,
`SQLite Error 1: 'no such table: Items'`) and confirmed fixed by exercising
`POST`/`GET /api/todos` end to end.

## Consequences

- A freshly generated `webapi` project builds and runs immediately with a working,
  migrated database: no external services, no manual `dotnet ef database update`.
- The Application layer stays provider-agnostic: it depends only on
  `IApplicationDbContext`, never a concrete `DbContext` type, so swapping providers is
  confined to Infrastructure and configuration.
- SQLite is not production-appropriate for every deployment target (limited
  concurrent-write support, no built-in replication); it's a local-development default,
  not a production recommendation.
- A known, accepted, non-blocking transitive vulnerability exists:
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (pulled in by `Microsoft.EntityFrameworkCore.Sqlite`
  10.0.9) is flagged by GHSA-2m69-gcr7-jv3q, with no patched version published upstream
  yet. Tracked via a comment in `templates/webapi/Directory.Packages.props`; CI is not
  expected to fail on it.
