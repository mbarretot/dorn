# 0013. Scaffolded CI Workflow

## Status

Accepted

## Context

`dorn new webapi` produces a project with four test tiers (ADR 0012), a database
provider choice (ADR 0011), and an orchestrator choice, but emits no GitHub Actions
workflow and pins no .NET SDK version. The workflow needs Ubuntu/Windows parity with
dorn's own CI, must exercise all four test tiers, avoid a combinatorial blow-up across
`os x orchestrator x database`, and stay extensible for future database providers.

## Decision

Emit one committed `.github/workflows/ci.yml` for every `webapi` scaffold, plus a static,
unconditional `global.json` at the generated repository root pinning the same SDK version
as dorn's own root `global.json` (`10.0.301`, `rollForward: latestFeature`).

### Workflow shape

| Aspect | Value |
|---|---|
| Triggers | `push`, `pull_request`, `workflow_dispatch` only |
| Permissions | Read-only (`contents: read`) |
| Concurrency | Scoped per ref, cancels in-progress runs |
| Matrix | `os` (`ubuntu-latest`/`windows-latest`) × `orchestrator` (`aspire`/`docker-compose`/`none`), six cells |
| Build/test sequence | Mirrors dorn's own: checkout → setup-dotnet → cache → restore → build (`-c Release --no-restore`, `-maxCpuCount:1 -nodeReuse:false`) → test, plus Ubuntu-only ReportGenerator coverage aggregation |
| Out of scope | `dotnet ef`, `dotnet pack`, `dotnet nuget push`, Dependabot/badges, publishing |

### Database provider gating

A committed `.github/config/db-provider.txt` marker (via `template.json` rename/exclude
modifiers keyed on `UseSqlServer`) represents the provider separately from the matrix. A
`configuration` job reads it into a job output before the matrix job starts.

### Test execution and tier exclusion

Default: a single solution-wide `dotnet test` per matrix cell. Opt-in tier exclusion via
`workflow_dispatch.inputs.exclude_tiers` runs one `dotnet test` per non-excluded tier
project (`Application`, `Integration`, `Functional`, `Architecture`), gated with
`if: !contains(inputs.exclude_tiers, '<Tier>')`. No `--filter`/trait machinery needed for
either path.

### SQL Server startup: ordinary steps, not a `services:` block

Database setup for the `sqlserver` marker is two ordinary job steps, both gated by
`if: runner.os == 'Linux' && needs.configuration.outputs.db == 'sqlserver'`: "Start SQL
Server (Linux)" runs `docker run mcr.microsoft.com/azure-sql-edge`, and "Wait for SQL
Server to be healthy (Linux)" polls with `sqlcmd -Q "select 1"`. A `services:` block was
rejected: those entries don't accept a per-service `if:` key, and service containers only
start on Linux, so a shared block on a matrix including `windows-latest` would attempt to
start on every Windows cell and fail. Ordinary steps support `if:` directly, producing the
identical outcome with no `sqlserver` cell running on Windows.
`Testcontainers.MsSql`, which `PersistenceTestFixture` (ADR 0012) uses internally, is
never invoked directly from the workflow.

### `actionlint` intentionally omitted

Optional per spec; dorn's own CI doesn't run it either, and the YAML-structural tests
plus a real generate-build-test smoke already give deterministic coverage without a
network/tool dependency.

## Consequences

- Every generated project gets a working, pinned-SDK CI workflow on first push.
- Provider growth stays linear: adding a database provider means one more marker value
  and one more conditional step pair, not a new matrix axis or duplicated workflow file.
- SQLite stays zero-secret and Docker-free in CI, matching its zero-config
  generation-time behavior (ADR 0011); only the `sqlserver` marker on a Linux runner ever
  starts a container.
- The step-based SQL Server startup is arguably more robust than a `services:` block:
  non-matching matrix cells never attempt container startup at all.
- Windows + SQL Server remains best-effort and untested by the cheapest smoke path:
  GitHub-hosted `windows-latest` runners provide no Docker host, so
  `PersistenceTestFixture` fails to start SQL Server there. Accepted, documented caveat;
  the Windows cell still validates build/restore and non-database tiers.
- The scaffolded `global.json` is a one-time static copy, not a live link: a project
  generated today keeps SDK `10.0.301` even after dorn's own root `global.json` later
  bumps. Keeping generated projects current is deferred to `dorn upgrade` (backlog #10).
- The orchestrator axis validates policy symmetry, but any single generated repository
  contains only one chosen orchestrator, so its three matrix cells build and test the
  same solution three times. Accepted for now; collapsing it is a possible future
  simplification.
