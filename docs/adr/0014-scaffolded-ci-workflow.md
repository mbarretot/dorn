# 0014. Scaffolded CI Workflow

## Status

Accepted

## Context

`dorn new webapi` produces a project with four test tiers (ADR 0013), a choice of
database provider (ADR 0012), and a choice of orchestrator, but emits no GitHub Actions
workflow at all. A generated project's first push has no CI, and nothing pins the .NET
SDK version a generated project builds against — `setup-dotnet`'s `global-json-file`
option would point at a file that doesn't exist. The workflow needs to preserve
Ubuntu/Windows parity with dorn's own CI, exercise all four generated test tiers, avoid a
combinatorial blow-up across `os x orchestrator x database`, and stay extensible when
another database provider (e.g. PostgreSQL) arrives.

## Decision

Emit one committed `.github/workflows/ci.yml` for every `webapi` scaffold, plus a static,
unconditional `global.json` at the generated repository root pinning the same SDK version
as dorn's own root `global.json` (`10.0.301`, `rollForward: latestFeature`). The workflow
uses a six-cell `os x orchestrator` matrix (`ubuntu-latest`/`windows-latest` x
`aspire`/`docker-compose`/`none`); database provider is represented separately by a
committed `.github/config/db-provider.txt` marker, emitted through `template.json`
rename/exclude modifiers keyed on `UseSqlServer`. A `configuration` job checks out the
repository and reads the marker into a job output before the matrix job starts, so
database setup can be gated on it. Mirrors dorn's own checkout/setup-dotnet/cache/restore
(`-maxCpuCount:1 -nodeReuse:false`)/build(`-c Release --no-restore`) sequence and
Ubuntu-only ReportGenerator coverage aggregation. Triggers are `push`, `pull_request`, and
`workflow_dispatch` only (no `schedule`, no path filters); permissions are read-only
(`contents: read`); concurrency is scoped per ref and cancels in-progress runs. The
workflow does not run `dotnet ef`, `dotnet pack`, `dotnet nuget push`, or anything
Dependabot/badge-related — publishing is out of scope.

Test execution defaults to a single solution-wide `dotnet test` per matrix cell. Tier
exclusion is opt-in via the `workflow_dispatch.inputs.exclude_tiers` input: when supplied,
the workflow instead runs one `dotnet test` invocation per non-excluded tier project
(`Application`, `Integration`, `Functional`, `Architecture`), gated with
`if: !contains(inputs.exclude_tiers, '<Tier>')`. No `--filter`/trait machinery is needed
for either path.

Database setup for the `sqlserver` marker is two ordinary job steps, not a `services:`
block: "Start SQL Server (Linux)" runs `docker run mcr.microsoft.com/azure-sql-edge`, and
"Wait for SQL Server to be healthy (Linux)" polls with `sqlcmd -Q "select 1"` until the
container answers. Both are gated by an identical step-level
`if: runner.os == 'Linux' && needs.configuration.outputs.db == 'sqlserver'`. A
`services:`-block design was considered first but is invalid GitHub Actions schema for
this job: `services:` entries do not accept a per-service `if:` key, and service
containers only start on Linux runners — so a shared `services:` block on a matrix that
includes `windows-latest` would attempt to start on every Windows cell and fail. Ordinary
steps do support `if:` and produce the identical outcome (image reference present, health
check precedes the test step, no container process for SQLite or for any Windows cell),
so no `sqlserver` cell has to run on Windows at all — a comment on the matching step
documents why, since GitHub-hosted `windows-latest` runners don't provide a Docker host
for `PersistenceTestFixture` (ADR 0013's `Integration.Tests` tier) to fall back to.
`Testcontainers.MsSql`, which that fixture uses internally as a .NET library, is never
invoked directly from the workflow.

`actionlint` is intentionally omitted: it's optional per the originating spec, dorn's own
CI doesn't run it, and the YAML-structural test coverage plus a real generate-build-test
smoke (cheapest cell: EF Core x none x SQLite) already gives deterministic coverage
without adding a network/tool dependency to the test suite.

## Consequences

- Every generated project gets a working, pinned-SDK CI workflow on first push, instead of
  needing manual GitHub Actions authoring or a `setup-dotnet` step pointed at a
  nonexistent `global.json`.
- Provider growth stays linear: adding a database provider means one more marker value and
  one more conditional step pair, not a new matrix axis or a duplicated workflow file.
- SQLite stays zero-secret and Docker-free in CI, matching its zero-config generation-time
  behavior (ADR 0012); only the `sqlserver` marker on a Linux runner ever starts a
  container.
- The step-based SQL Server startup is schema-valid GitHub Actions and is arguably more
  robust than a `services:` block would have been, since non-matching matrix cells never
  attempt container startup at all rather than merely being gated by a field GitHub
  Actions doesn't honor per-service.
- Windows + SQL Server remains best-effort and untested by the cheapest smoke path:
  GitHub-hosted `windows-latest` runners provide no Docker host, so
  `PersistenceTestFixture` fails to start SQL Server there. This is an accepted,
  documented runtime caveat, not a workflow bug — do not "fix" it by removing the Windows
  matrix cell, since the cell still validates the build/restore/non-database-dependent
  tiers on Windows.
- The scaffolded `global.json` is a one-time static copy of dorn's own root `global.json`,
  not a live link. A project generated today keeps SDK `10.0.301` even after dorn's own
  root `global.json` later bumps; keeping generated projects current with future SDK pins
  is deferred to `dorn upgrade` (backlog item #10), not this feature.
- The orchestrator axis (`aspire`/`docker-compose`/`none`) validates policy symmetry, but
  any single generated repository contains only one chosen orchestrator, so its three
  matrix cells build and test the same generated solution three times. This redundancy is
  accepted for now; collapsing it is a possible future simplification.
