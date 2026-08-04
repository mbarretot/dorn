# 0015. PostgreSQL as a First-Class Database Provider

## Status

Accepted

## Context

ADR 0012 made SQL Server a first-class, generation-time `--database` choice alongside the
SQLite default, specifically so a contributor never has to hand-edit generated source to
get an Aspire-hosted database working. That same ADR explicitly left PostgreSQL out of
scope: `docs/templates/webapi.md` documented a manual swap (replace the EF Core package,
change the `options.Use...(...)` call, hand-author a connection string, regenerate
migrations by hand) rather than a `--database postgres` flag.

PostgreSQL is one of the most common production database engines and — unlike SQL
Server — is free and open-source, so the manual-swap friction ADR 0012 already removed for
SQL Server remained for a database many contributors are more likely to actually run. The
`webapi` template's existing binary `UseSqlServer` branching (`#if (UseSqlServer)` /
`#else` meaning "SQLite") was also a latent trap for adding *any* third provider: every
`#else` silently emitted SQLite wiring rather than failing if a provider went unhandled.

## Decision

Add `postgres` as a third `DatabaseProvider` choice, at parity with `sqlserver`
(`dorn new webapi MyApp --database sqlite|sqlserver|postgres`), delivered as three
independently mergeable, stacked PRs to keep each review under the project's 400-line
budget:

1. **Exhaustive three-way branching first, behavior-preserving.** Before `postgres` became
   selectable, every `UseSqlServer`-implies-`#else`-means-SQLite site (`template.json`,
   `ServiceCollectionExtensions.cs`, `DapperContext.cs`, `AppHost.cs`, `appsettings.json`,
   `.csproj` MSBuild conditions, `Directory.Build.props`, `Directory.Packages.props`,
   `PersistenceTestFixture.cs`) was converted to an explicit `UseSqlite` / `UseSqlServer` /
   `UsePostgres` three-way branch with no `#else` / no `'!= True'` fallback anywhere, so an
   unhandled provider is a compile/restore error rather than a silent SQLite default. This
   refactor shipped alone first and was verified to produce byte-identical `sqlite`/
   `sqlserver` output before `postgres` was introduced as a choice.
2. **PostgreSQL wiring at parity with SQL Server.** `Npgsql.EntityFrameworkCore.PostgreSQL`
   (EF Core provider) and `Npgsql` (ADO.NET driver, used directly by the Dapper path), both
   pinned to `10.0.3`; `Aspire.Hosting.PostgreSQL` `13.4.6`, the same version already pinned
   for `Aspire.Hosting.SqlServer`; `Testcontainers.PostgreSql` `4.11.0`, conditioned on
   `UsePostgres` exactly like `Testcontainers.MsSql` is conditioned on `UseSqlServer`.
   `AppHost.cs` adds `builder.AddPostgres("postgres").AddDatabase("CleanArchWebApi")` and
   wires it via `WithReference(postgres)`, mirroring `AddSqlServer` verbatim.
3. **Real, EF-generated migrations.** `Infrastructure/Persistence/Migrations/Postgres/`
   contains actual `dotnet ef migrations add` output — never hand-written — generated
   against a genuinely live PostgreSQL instance and verified by applying it and inspecting
   the resulting schema, the same authoring discipline ADR 0012 established for the SQL
   Server migration set. The `template.json` rename/exclude modifier pattern that
   guaranteed exactly one `ApplicationDbContextModelSnapshot` for two providers now
   guarantees it for three.
4. **Validator, CLI, and testing enum parity.**
   `Dorn.Core.Validation.DatabaseProviderValidator` accepts `postgres`; the interactive
   prompt and `Dorn.Cli.Testing.DatabaseProvider` enum represent it identically to the
   other two providers. `AspireResourceNameValidator`, previously hard-coding the
   `"--database sqlserver"` string in its error message, is parameterized to the actual
   provider so it reads correctly for `postgres` too.
5. **Integration tier via Testcontainers.** `PersistenceTestFixture` gains a `UsePostgres`
   branch using `Testcontainers.PostgreSql`, mirroring the existing `Testcontainers.MsSql`
   branch, so the Integration tier runs real migrations and CRUD against a real Postgres
   container when the project was generated with `--database postgres`. The Functional
   tier stays SQLite-only regardless of `--database`, unchanged from ADR 0012/0013.
6. **CI gating reuses ADR 0014's marker mechanism, no new matrix axis.** A
   `db-provider.txt.tpl-postgres` marker file, combined with the existing three-way
   rename/exclude modifier, plugs into ADR 0014's `configuration` job exactly like the
   `sqlite`/`sqlserver` markers already do. `ci.yml` gains four Linux-gated steps mirroring
   the SQL Server pair: a disposable-password step (`openssl rand -base64 24`, never a
   committed literal — mirroring the GitGuardian-driven discipline the SQL Server step
   already used), `docker run postgres:17`, a `pg_isready` wait loop, and a Windows
   best-effort caveat step. Unlike SQL Server, which needs two different images
   (`azure-sql-edge` in CI to avoid a runner SIGSEGV, `mssql/server:2022-latest` in compose
   and Testcontainers), PostgreSQL uses `postgres:17` identically in CI, compose, and the
   Testcontainers fixture — one image, three contexts.
7. **Docker Compose.** `docker-compose.Postgres.yml` mirrors `docker-compose.SqlServer.yml`
   — a `postgres:17` service with a `pg_isready` healthcheck and a named volume, and a
   `webapi` service `ConnectionStrings__CleanArchWebApi` override pointing at the compose
   DNS name — wired into the same `UseCompose && Use<Provider>` modifier pattern.
8. **Docs.** The manual Postgres-swap section in `docs/templates/webapi.md` is removed and
   replaced with generic guidance for engines that are still a manual swap (e.g. MySQL,
   Oracle) — the same five mechanical touch-points, without Postgres-specific text, since
   `postgres` is no longer one of them. `README.md`'s `--database` flag table and
   `docs/templates/webapi.md`'s flag references list `postgres` alongside `sqlite`/
   `sqlserver` throughout.

## Consequences

- `dorn new webapi MyApp --database postgres` now behaves exactly like `--database
  sqlserver` did after ADR 0012: zero Docker dependency at generation time, Docker only
  required at `dotnet run --project src/MyApp.AppHost` time for the Aspire-hosted
  container.
- Three authored migration sets under source control (`Migrations/Sqlite/`,
  `Migrations/SqlServer/`, `Migrations/Postgres/`) instead of two — the same bounded
  maintenance cost ADR 0012 accepted for going from one to two, now extended to three, in
  exchange for every provider being immediately runnable with no post-generation manual
  step.
- Adding a fourth provider in the future is bounded, not open-ended: the exhaustive
  three-way (now N-way) branching this feature established means an unhandled provider
  fails to compile/restore rather than silently generating SQLite wiring — a stronger
  guarantee than the binary branching ADR 0012 originally shipped with.
- `docs/adr/0012-database-provider-selection.md` is left as-is (still `Accepted`, not
  edited or superseded), mirroring how that ADR itself left ADR 0005 unedited when it added
  SQL Server alongside SQLite. Its "PostgreSQL remains a manual swap" consequence bullet is
  now historical — this ADR is the authoritative record of that change, not an edit to
  0012's original reasoning.
- MySQL, Oracle, and any other engine remain a manual swap; this feature does not add a
  fourth first-class choice or provider-agnostic migrations.
- Docker was unavailable in the implementation sandbox: the Postgres migration was
  generated and verified against a genuinely live, non-containerized local PostgreSQL
  instance instead, and the `Testcontainers.PostgreSql`-driven Integration tier run was
  verified by compilation only, not a live container spin-up. Both are flagged as residual
  verification gaps to close out in a Docker-capable environment (a contributor's machine
  or CI) before treating end-to-end Integration-tier coverage as fully proven.
