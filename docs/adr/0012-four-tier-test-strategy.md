# 0012. Four-Tier Test Strategy for the `webapi` Template

## Status

Accepted

## Context

The `webapi` template shipped with a single test project,
`CleanArchWebApi.Application.Tests`, covering command/query handlers against an
in-memory SQLite connection (`EnsureCreated`, not real migrations). That leaves several
concerns unverified: `EnsureCreated()` never runs the actual migration files, so a broken
migration wouldn't be caught until a real deployment; ADR 0011's `sqlserver` provider was
never exercised against a real instance; layering rules (Domain depends on nothing,
Application doesn't depend on Infrastructure/WebApi) are documented as prose only, so a
stray `using` compiles silently; and no test exercised the Minimal API endpoints as HTTP
requests (routing, model binding, FluentValidation's pipeline behavior, serialization).

## Decision

Add three test projects alongside `Application.Tests`, so every generated project ships
four tiers:

1. **`Application.Tests` (Unit)**: unchanged. Fast, provider-agnostic, SQLite in-memory,
   `EnsureCreated()`. Exercises handler logic and domain event publication.
2. **`Integration.Tests`**: exercises the real selected `DatabaseProvider` via
   `Database.MigrateAsync()` against a live database: a unique SQLite file when
   `sqlite`, a real SQL Server container (`Testcontainers.MsSql`) when `sqlserver`. Proves
   the checked-in migrations apply cleanly.
3. **`Architecture.Tests`**: fitness functions enforcing the layering rules as executable
   assertions, using **TngTech.ArchUnitNET.xUnit**.
4. **`Functional.Tests`**: `WebApplicationFactory<Program>`-based HTTP round-trip tests
   against the real Minimal API endpoints, forcing SQLite regardless of the generated
   `DatabaseProvider`, since this tier's job is the HTTP pipeline, not provider fidelity.

All four tiers stay under the existing `IncludeTests` symbol.

### TngTech.ArchUnitNET.xUnit over NetArchTest.Rules

The first implementation used **NetArchTest.Rules**, wrongly assumed current: its last
release is 1.3.2 (May 2021), while **ArchUnitNET** ships regularly (0.13.3 as of this
writing). Corrected to `TngTech.ArchUnitNET.xUnit`:

- `ArchLoader().LoadAssembliesIncludingDependencies(...)` also resolves
  referenced-but-not-directly-loaded types (e.g. EF Core), needed for
  `Domain_ShouldNot_DependOnEntityFrameworkCore` to detect a real violation rather than
  passing vacuously.
- `.Check(architecture)` throws `FailedArchRuleException` (an `XunitException`) naming
  the violating type and target directly.
- One naming collision: this project's namespace (`CleanArchWebApi.Architecture.Tests`)
  shadows ArchUnitNET's `Architecture` type, aliased in `GlobalUsings.cs`
  (`ArchitectureModel = ArchUnitNET.Domain.Architecture`).
- Matching an open generic interface (`IRequestHandler<,>`) isn't well supported by the
  fluent predicate API, so `RequestHandlers_Should_ResideInApplicationAssembly` uses
  plain reflection instead.
- Verified the rules have teeth: temporarily referencing `Microsoft.EntityFrameworkCore`
  inside `CleanArchWebApi.Domain` made the rule fail with a precise violation message,
  then the reference was reverted.

### Keeping Dorn's own CI Docker-free

`Dorn.slnx` (the dev repo's own solution) references the raw, un-generated
`templates/webapi/src/*/*.csproj` and `templates/webapi/tests/*/*.csproj` files
directly, and CI runs `dotnet test Dorn.slnx` on Ubuntu and Windows with no Docker
service. `UseSqlServer` is a template-engine-computed symbol, undefined outside
generation, so both the MSBuild `Condition="'$(UseSqlServer)' == 'True'"` and the C#
`#if (UseSqlServer)` evaluate false/excluded on the raw build. `Testcontainers.MsSql`,
the only Docker-dependent dependency this change introduces, is referenced exclusively
behind that same condition in `CleanArchWebApi.Integration.Tests`, mirroring the existing
`Microsoft.EntityFrameworkCore.SqlServer` pattern. The raw build therefore never restores
it or compiles the SQL Server fixture branch.

## Consequences

- Every generated project gets migration-fidelity, layering, and HTTP-level coverage it
  didn't have before, at the cost of four test projects instead of one.
- `Integration.Tests` is the only tier that can require Docker, and only with
  `--database sqlserver`; the default `sqlite` never touches Docker in any tier.
- `Functional.Tests` always uses SQLite, even in `sqlserver` generations, a deliberate
  scope choice: HTTP-pipeline correctness doesn't depend on the database behind it, but
  it can't catch a provider-specific bug that only manifests over HTTP;
  `Integration.Tests` owns that.
- `Architecture.Tests` is noticeably slower (~4-5s) than a NetArchTest-based equivalent,
  since ArchUnitNET pulls in more transitive dependencies and loads a wider assembly
  graph. Accepted cost for a rule that can actually detect violations.
