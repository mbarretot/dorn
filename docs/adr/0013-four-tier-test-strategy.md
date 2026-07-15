# 0013. Four-Tier Test Strategy for the `webapi` Template

## Status

Accepted

## Context

The `webapi` template shipped with a single test project,
`CleanArchWebApi.Application.Tests`, covering command/query handlers against an in-memory
SQLite connection (`EnsureCreated`, not real migrations). That leaves several concerns
unverified in every generated project:

- **Migration fidelity.** `EnsureCreated()` builds the schema directly from the current EF
  Core model and never runs the actual migration files under
  `Persistence/Migrations/Sqlite|SqlServer/`. A broken migration (bad column type, missing
  index, provider-specific SQL) would not be caught until a real deployment.
- **Provider-specific behavior.** ADR 0012 lets a project pick `DatabaseProvider=sqlserver`.
  Nothing exercised the generated code against a real SQL Server instance; SQL Server and
  SQLite differ enough (types, `MigrateAsync` behavior, connection semantics) that
  SQLite-only tests provide no confidence for that path.
- **Layering drift.** README.md and ADR 0010 document layering rules (Domain depends on
  nothing, Application doesn't depend on Infrastructure/WebApi, etc.) as prose. Nothing
  enforced them; a stray `using` statement compiles silently.
- **HTTP-level behavior.** No test exercised the Minimal API endpoints as HTTP requests —
  routing, model binding, FluentValidation's pipeline behavior, and JSON serialization were
  only implicitly covered through handler unit tests that never touch `Program.cs`.

## Decision

Add three test projects alongside the existing `Application.Tests`, so every generated
project ships four tiers, each with a distinct purpose and its own tradeoffs:

1. **`Application.Tests` (Unit)** — unchanged. Fast, provider-agnostic, SQLite in-memory,
   `EnsureCreated()`. Exercises handler logic and domain event publication.
2. **`Integration.Tests`** — exercises the real selected `DatabaseProvider` via
   `Database.MigrateAsync()` against a live database: a unique SQLite file when
   `DatabaseProvider=sqlite`, a real SQL Server container (via `Testcontainers.MsSql`) when
   `DatabaseProvider=sqlserver`. This is the tier that actually proves the checked-in
   migrations apply cleanly.
3. **`Architecture.Tests`** — fitness functions enforcing the layering rules as executable
   assertions instead of prose, using **TngTech.ArchUnitNET.xUnit**.
4. **`Functional.Tests`** — `WebApplicationFactory<Program>`-based HTTP round-trip tests
   against the real Minimal API endpoints, forcing SQLite regardless of the generated
   `DatabaseProvider` (see the "Docker-free by construction" section below) since this tier's
   job is the HTTP pipeline, not provider fidelity.

All four tiers stay under the existing `IncludeTests` symbol; no new template symbol was
added; `(!IncludeTests)` excluding `tests/**` already covers all four.

### TngTech.ArchUnitNET.xUnit over NetArchTest.Rules

The first implementation of this ADR used **NetArchTest.Rules**, on the mistaken
assumption that it was the more current option. That was wrong: NetArchTest.Rules' last
release is 1.3.2 (May 2021), while **ArchUnitNET** ships regular releases (0.13.3 as of
this writing) and is the actively maintained option. Corrected to
`TngTech.ArchUnitNET.xUnit`:

- `ArchLoader().LoadAssembliesIncludingDependencies(...)` builds an `Architecture` model
  that also resolves referenced-but-not-directly-loaded types (e.g. EF Core), which the
  `Domain_ShouldNot_DependOnEntityFrameworkCore` rule needs to detect a real violation
  rather than passing vacuously against an empty type set.
- The `TngTech.ArchUnitNET.xUnit` package adds a `.Check(architecture)` extension that
  throws `FailedArchRuleException` (an `XunitException`) with the violating type and target
  named in the message — no manual result-object assertion needed.
- One naming collision: this project's own namespace (`CleanArchWebApi.Architecture.Tests`)
  shadows ArchUnitNET's `Architecture` type, so `GlobalUsings.cs` aliases it explicitly
  (`ArchitectureModel = ArchUnitNET.Domain.Architecture`).
- Same limitation as NetArchTest: matching an *open generic* interface
  (`IRequestHandler<,>`) isn't well supported by the fluent predicate API, so
  `RequestHandlers_Should_ResideInApplicationAssembly` still uses plain reflection instead
  — a real fitness function, just not phrased through ArchUnitNET's DSL for that one case.
- Verified the rules have teeth, not just that they compile: temporarily added a
  `Microsoft.EntityFrameworkCore` reference inside `CleanArchWebApi.Domain` and confirmed
  `Domain_ShouldNot_DependOnEntityFrameworkCore` failed with a precise violation message,
  then reverted it.

### The `UseSqlServer`-defaults-false trick keeps Dorn's own CI Docker-free

`Dorn.slnx` (the actual dev repo solution, not a generated project) references the raw,
un-generated `templates/webapi/src/*/*.csproj` and `templates/webapi/tests/*/*.csproj` files
directly, and `.github/workflows/ci.yml` runs `dotnet test Dorn.slnx` on both
`ubuntu-latest` and `windows-latest` with no Docker service configured. `UseSqlServer` is a
template-engine-computed symbol; outside of template generation it is simply undefined, so:

- Every MSBuild `Condition="'$(UseSqlServer)' == 'True'"` attribute (on `PackageReference`,
  `PackageVersion`, `Compile`) evaluates false.
- Every `#if (UseSqlServer)` C# preprocessor block (a real preprocessor directive, present
  in the raw source too) evaluates false.

`Testcontainers.MsSql` — the only Docker-dependent dependency introduced by this change —
is referenced exclusively behind that same `Condition`/`#if` pair in
`CleanArchWebApi.Integration.Tests`, mirroring the existing pattern already used by
`Microsoft.EntityFrameworkCore.SqlServer` in `CleanArchWebApi.Infrastructure.csproj` (see
`Directory.Packages.props`). The raw build therefore never restores `Testcontainers.MsSql`
and never compiles the SQL Server fixture branch, keeping `dotnet test Dorn.slnx` Docker-free
exactly as it is today.

## Consequences

- Every generated project gets migration-fidelity, layering, and HTTP-level coverage it
  didn't have before, at the cost of four test projects to restore/build/run instead of one.
- `Integration.Tests` is the only tier that can require Docker, and only when the project is
  generated with `--database sqlserver`. `--database sqlite` (the default) never touches
  Docker in any tier.
- `Functional.Tests` always uses SQLite, even in `--database sqlserver` generations. This is
  a deliberate scope choice — HTTP-pipeline correctness doesn't depend on which relational
  database sits behind it — but it does mean `Functional.Tests` cannot catch a
  provider-specific bug that only manifests over HTTP; `Integration.Tests` is the tier
  responsible for provider-specific behavior.
- `Architecture.Tests` pulls in ArchUnitNET's transitive dependencies (Mono.Cecil among
  them) and loads a wider assembly graph via `LoadAssembliesIncludingDependencies`, making
  it noticeably slower (~4-5s) than a NetArchTest-based equivalent would have been — an
  accepted cost for a rule that can actually detect violations across assembly boundaries.
