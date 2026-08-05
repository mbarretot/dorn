# 0011. Database Provider Selection at Generation Time

## Status

Accepted

## Context

ADR 0005 chose EF Core + SQLite as the `webapi` template's only persistence option, and
documents SQL Server/PostgreSQL as a possible future manual swap: edit
`ServiceCollectionExtensions.cs`, change the connection string by hand, regenerate
migrations yourself. That's no longer enough once a contributor wants SQL Server without
post-generation surgery; a scaffolding tool's golden path shouldn't require it. Making
the provider a generation-time choice
(`dorn new webapi MyApp --database sqlite|sqlserver`) turns that manual swap into
something the Template Engine does correctly by construction, the same way
`IncludeTests` already does for the test project.

SQL Server also needed to be immediately runnable: the `webapi` template wires in .NET
Aspire (`AppHost`/`ServiceDefaults`), whose resource model can host a SQL Server
container directly, avoiding the "external setup" friction ADR 0005 chose SQLite to
avoid.

## Decision

Add a `DatabaseProvider` choice parameter to
`templates/webapi/.template.config/template.json` (`sqlite` default, `sqlserver` the
alternative), plus a computed `UseSqlServer` boolean the rest of the template's
conditional mechanics compare against:

- **`Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`**: `#if
  (UseSqlServer)` switches between `options.UseSqlServer(...)` and `options.UseSqlite(...)`.
  The connection string key differs on purpose: Aspire's `WithReference(sql)` injects the
  resolved connection string under the resource name `"CleanArchWebApi"`, no Aspire
  client package needed in Infrastructure.
- **`AppHost.cs`**: `#if (UseSqlServer)` adds
  `builder.AddSqlServer("sql").AddDatabase("CleanArchWebApi")` and wires it via
  `WithReference(sql)`; the `sqlite` branch is unchanged.
- **`.csproj` files**: MSBuild `Condition="'$(UseSqlServer)' == 'True'"` toggles
  `Microsoft.EntityFrameworkCore.Sqlite` vs. `Microsoft.EntityFrameworkCore.SqlServer` in
  Infrastructure, and conditionally adds `Aspire.Hosting.SqlServer` (pinned to `13.4.6`,
  matching `Aspire.Hosting.AppHost`) in AppHost.
- **`appsettings.json`**: a `//#if (!UseSqlServer)` block keeps the static
  `"Default": "Data Source=app.db"` connection string only for SQLite, since SQL Server's
  is injected by Aspire at runtime.
- **Dual migrations**: `Infrastructure/Persistence/Migrations/` has two real,
  provider-generated sibling folders, `Sqlite/` and `SqlServer/`. Two
  `sources[0].modifiers` entries in `template.json` rename whichever matches the chosen
  provider up to `Migrations/` and exclude the other, so exactly one
  `ApplicationDbContextModelSnapshot` ever lands in generated output. The SQL Server
  migration was generated with the real `dotnet ef migrations add` tool against a scratch
  copy, not hand-written.
- **CLI**: `dorn new webapi MyApp --database sqlite|sqlserver`
  (`NewWebApiSettings.Database`, validated by
  `Dorn.Core.Validation.DatabaseProviderValidator`). Omitting `--database` prompts
  interactively (`SelectionPrompt<string>` listing `sqlite` first), and falls back
  silently to `sqlite` in a non-interactive session (CI).

`IApplicationDbContext` is untouched: already provider-agnostic per ADR 0005.

## Consequences

- `dorn new webapi MyApp` (no flag) behaves exactly as before: SQLite, interactive prompt
  only in a real terminal, sqlite listed first.
- `--database sqlserver` needs Docker (for the Aspire-hosted container) to run, but
  builds and generates with zero Docker dependency at generation time.
- Two authored migration sets under source control instead of one, a small, bounded
  maintenance cost in exchange for both providers being immediately runnable.
- PostgreSQL remains a manual swap; this feature does not add a third first-class choice.
- `docs/adr/0005-ef-core-sqlite-default-persistence.md` is left as-is; this ADR only adds
  a first-class alternative alongside it.
