# 0012. Database Provider Selection at Generation Time

## Status

Accepted

## Context

ADR 0005 chose EF Core + SQLite as the `webapi` template's only persistence option,
specifically because SQLite requires zero external setup — `dotnet run` against a freshly
generated project produces a working, migrated database immediately. ADR 0005 documents
SQL Server/PostgreSQL as a possible "future manual swap," but leaves that swap entirely
manual: edit `ServiceCollectionExtensions.cs`, change the connection string by hand, and
regenerate migrations yourself.

That's a reasonable default, but it stops being enough once a contributor wants SQL
Server for a generated project without hand-editing generated source immediately after
`dorn new webapi` finishes — the whole point of a scaffolding tool is that the golden path
doesn't require post-generation surgery. Making the provider a first-class,
generation-time choice (`dorn new webapi MyApp --database sqlite|sqlserver`) turns that
manual swap into something the Template Engine does correctly by construction, the same
way `IncludeTests` already does for the test project.

SQL Server also needed to be immediately runnable out of the box, not just "point your own
server at this connection string" — the `webapi` template already wires in .NET Aspire
(`AppHost`/`ServiceDefaults`, see the AppHost/ServiceDefaults section of
`docs/templates/webapi.md`) as the primary way contributors run a generated project
locally, and Aspire's resource model can host a SQL Server container directly. Requiring a
contributor to separately stand up their own SQL Server instance would have reintroduced
exactly the "external setup" friction ADR 0005 chose SQLite specifically to avoid.

## Decision

Add a `DatabaseProvider` choice parameter to `templates/webapi/.template.config/template.json`
(`sqlite` default, `sqlserver` the alternative), plus a computed `UseSqlServer` boolean
that the rest of the template's conditional mechanics compare against:

- **`Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`** — `#if
  (UseSqlServer)` switches between `options.UseSqlServer(configuration.GetConnectionString("CleanArchWebApi"))`
  and `options.UseSqlite(configuration.GetConnectionString("Default"))`. The connection
  string key differs on purpose: when Aspire's `WithReference(sql)` wires a project to a
  hosted resource named `"CleanArchWebApi"`, Aspire injects the resolved connection string
  into the referencing project's configuration under that same resource name — no Aspire
  client package needed in Infrastructure, just plain `IConfiguration`, keeping the
  layer's only dependency on `Microsoft.Extensions.Configuration.Abstractions` as today.
- **`AppHost.cs`** — `#if (UseSqlServer)` adds `builder.AddSqlServer("sql").AddDatabase("CleanArchWebApi")`
  and wires the WebApi project to it via `WithReference(sql)`; the `sqlite` branch is
  unchanged from before this feature (AppHost only orchestrates the WebApi project).
- **`.csproj` files** — MSBuild `Condition="'$(UseSqlServer)' == 'True'"` /
  `!= 'True'` toggles `Microsoft.EntityFrameworkCore.Sqlite` vs.
  `Microsoft.EntityFrameworkCore.SqlServer` in Infrastructure, and conditionally adds
  `Aspire.Hosting.SqlServer` (pinned to the same `13.4.6` version as the already-pinned
  `Aspire.Hosting.AppHost`) in AppHost.
- **`appsettings.json`** — a `//#if (!UseSqlServer)` / `//#endif` comment-block keeps the
  static `"Default": "Data Source=app.db"` connection string only for SQLite; SQL
  Server's connection string is never static, since Aspire injects it at runtime.
- **Dual migrations** — `Infrastructure/Persistence/Migrations/` has two real,
  provider-generated sibling folders, `Sqlite/` and `SqlServer/`, each with its own
  `InitialCreate` migration and `ApplicationDbContextModelSnapshot`. Two `sources[0].modifiers`
  entries in `template.json` (alongside the existing `IncludeTests` one) rename whichever
  folder matches the chosen provider up to `Migrations/` and exclude the other folder
  entirely, so exactly one `ApplicationDbContextModelSnapshot` ever physically lands in
  generated output — never zero, never two. The SQL Server migration was generated with
  the real `dotnet ef migrations add` tool against a scratch copy of the project
  temporarily pointed at `UseSqlServer(...)`, not hand-written, so the SQL type mappings
  (`nvarchar`, `bit`, `uniqueidentifier`, etc.) are the same ones EF Core itself would
  produce for a real project.
- **CLI** — `dorn new webapi MyApp --database sqlite|sqlserver`
  (`NewWebApiSettings.Database`, validated by `Dorn.Core.Validation.DatabaseProviderValidator`,
  mirroring `ProjectNameValidator`'s shape). Omitting `--database` prompts interactively
  (`IAnsiConsole.Profile.Capabilities.Interactive`, a `SelectionPrompt<string>` listing
  `sqlite` first) when the session supports it, and silently falls back to `sqlite`
  otherwise (non-interactive/CI), so there is no hang waiting for input that will never
  arrive.

`IApplicationDbContext` (the Application-layer persistence port) is untouched — it was
already provider-agnostic per ADR 0005, and stays that way; this feature only changes what
Infrastructure and AppHost do with it.

## Consequences

- `dorn new webapi MyApp` (no flag) behaves exactly as before this change: SQLite,
  interactive prompt only in a real interactive terminal, sqlite listed first.
- `dorn new webapi MyApp --database sqlserver` produces a project that needs Docker (for
  the Aspire-hosted SQL Server container) to actually run, but builds and generates
  correctly with zero Docker dependency at generation time — Docker is only needed at
  `dotnet run --project src/MyApp.AppHost` time, not at `dorn new webapi` time.
- Two authored migration sets under source control (`Migrations/Sqlite/`,
  `Migrations/SqlServer/`) instead of one — a small, bounded maintenance cost (both need
  updating if `TodoItem`'s schema ever changes) in exchange for both providers being
  immediately runnable rather than one of them requiring a manual `dotnet ef migrations
  add` step post-generation.
- PostgreSQL remains a manual swap, exactly as ADR 0005 originally described — this
  feature does not add a third first-class choice. `docs/templates/webapi.md` keeps the
  manual PostgreSQL swap instructions, updated to reflect that the starting point may now
  be either the SQLite or the SQL Server migration set.
- `docs/adr/0005-ef-core-sqlite-default-persistence.md` is left as-is (still `Accepted`,
  not edited or superseded) — its SQLite-as-default rationale still holds; this ADR only
  adds a first-class alternative alongside it.
