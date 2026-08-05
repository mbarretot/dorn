# 0014. PostgreSQL as a First-Class Database Provider

## Status

Accepted

## Context

ADR 0011 made SQL Server a first-class, generation-time `--database` choice alongside the
SQLite default, so a contributor never has to hand-edit generated source to get an
Aspire-hosted database working. That ADR explicitly left PostgreSQL out of scope: a
manual swap (replace the EF Core package, change the `options.Use...(...)` call,
hand-author a connection string, regenerate migrations by hand).

PostgreSQL is one of the most common production database engines and, unlike SQL Server,
free and open-source, so the manual-swap friction ADR 0011 removed for SQL Server
remained for a database contributors are more likely to actually run. The template's
existing binary `UseSqlServer` branching (`#if (UseSqlServer)` / `#else` meaning
"SQLite") was also a latent trap for adding any third provider: every `#else` silently
emitted SQLite wiring rather than failing on an unhandled provider.

## Decision

Add `postgres` as a third `DatabaseProvider` choice, at parity with `sqlserver`
(`dorn new webapi MyApp --database sqlite|sqlserver|postgres`), delivered as three
independently mergeable, stacked PRs to keep each review under the project's 400-line
budget:

1. **Exhaustive three-way branching first, behavior-preserving.** Every
   `UseSqlServer`-implies-`#else`-means-SQLite site (`template.json`,
   `ServiceCollectionExtensions.cs`, `DapperContext.cs`, `AppHost.cs`, `appsettings.json`,
   `.csproj` MSBuild conditions, `Directory.Build.props`, `Directory.Packages.props`,
   `PersistenceTestFixture.cs`) was converted to an explicit `UseSqlite` / `UseSqlServer` /
   `UsePostgres` three-way branch with no fallback anywhere, before `postgres` became
   selectable, so an unhandled provider is a compile/restore error rather than a silent
   default. Verified to produce byte-identical `sqlite`/`sqlserver` output before
   `postgres` was introduced.
2. **PostgreSQL wiring at parity with SQL Server.** Packages:
   `Npgsql.EntityFrameworkCore.PostgreSQL` and `Npgsql` (ADO.NET driver for the Dapper
   path), both pinned to `10.0.3`; `Aspire.Hosting.PostgreSQL` `13.4.6` (same version as
   `Aspire.Hosting.SqlServer`); `Testcontainers.PostgreSql` `4.11.0`, conditioned on
   `UsePostgres` like `Testcontainers.MsSql` is on `UseSqlServer`. `AppHost.cs` adds
   `builder.AddPostgres("postgres").AddDatabase("CleanArchWebApi")` via
   `WithReference(postgres)`, mirroring `AddSqlServer` verbatim.
3. **Real, EF-generated migrations.** `Infrastructure/Persistence/Migrations/Postgres/`
   contains actual `dotnet ef migrations add` output, generated against a live PostgreSQL
   instance and verified by applying it and inspecting the schema. The
   `template.json` rename/exclude modifier pattern now guarantees exactly one
   `ApplicationDbContextModelSnapshot` for three providers.
4. **Validator, CLI, and testing enum parity.**
   `Dorn.Core.Validation.DatabaseProviderValidator`, the interactive prompt, and
   `Dorn.Cli.Testing.DatabaseProvider` all accept/represent `postgres` identically to the
   other two. `AspireResourceNameValidator`, previously hard-coding
   `"--database sqlserver"` in its error message, is parameterized to the actual provider.
5. **Integration tier via Testcontainers.** `PersistenceTestFixture` gains a `UsePostgres`
   branch mirroring the existing `Testcontainers.MsSql` branch. The Functional tier stays
   SQLite-only regardless of `--database`, unchanged from ADR 0011/0012.
6. **CI gating reuses ADR 0013's marker mechanism, no new matrix axis.** A
   `db-provider.txt.tpl-postgres` marker plugs into the `configuration` job exactly like
   the existing markers. `ci.yml` gains four Linux-gated steps mirroring the SQL Server
   pair: a disposable-password step (`openssl rand -base64 24`, never a committed
   literal), `docker run postgres:17`, a `pg_isready` wait loop, and a Windows
   best-effort caveat step. Unlike SQL Server, which needs two different images
   (`azure-sql-edge` in CI, `mssql/server:2022-latest` in compose and Testcontainers),
   PostgreSQL uses `postgres:17` identically in all three contexts.
7. **Docker Compose.** `docker-compose.Postgres.yml` mirrors
   `docker-compose.SqlServer.yml`: a `postgres:17` service with a `pg_isready`
   healthcheck and named volume, and a `webapi` service
   `ConnectionStrings__CleanArchWebApi` override, wired into the same
   `UseCompose && Use<Provider>` modifier pattern.
8. **Docs.** The manual Postgres-swap section in `docs/templates/webapi.md` is replaced
   with generic guidance for engines still a manual swap (e.g. MySQL, Oracle).
   `README.md`'s `--database` flag table lists `postgres` alongside `sqlite`/`sqlserver`
   throughout.

## Consequences

- `dorn new webapi MyApp --database postgres` behaves exactly like `--database
  sqlserver` did after ADR 0011: zero Docker dependency at generation time, Docker only
  required at `dotnet run --project src/MyApp.AppHost` time.
- Three authored migration sets under source control instead of two, the same bounded
  maintenance cost ADR 0011 accepted going from one to two, in exchange for every
  provider being immediately runnable.
- Adding a fourth provider is bounded: the exhaustive N-way branching means an unhandled
  provider fails to compile/restore rather than silently generating SQLite wiring.
- `docs/adr/0011-database-provider-selection.md` is left as-is; its "PostgreSQL remains a
  manual swap" bullet is now historical, this ADR is the authoritative record of the
  change.
- MySQL, Oracle, and any other engine remain a manual swap.
- Docker was unavailable in the implementation sandbox: the Postgres migration was
  generated and verified against a live, non-containerized local instance instead, and
  the `Testcontainers.PostgreSql`-driven Integration tier was verified by compilation
  only, not a live container spin-up. Both are flagged as residual verification gaps to
  close in a Docker-capable environment.
