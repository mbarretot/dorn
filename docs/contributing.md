# Contributing

Thanks for considering a contribution to Dorn. This document covers how to add a new
template, coding conventions, the verification loop to run before opening a PR, and
licensing.

## Adding a new template

`webapi` is the reference implementation — follow its pattern for a new template (the
next one on the roadmap is `ui`, currently just a placeholder at `templates/ui/README.md`):

1. Create `templates/<name>/` with its own `.template.config/template.json` (identity,
   `shortName`, `sourceName`, any `symbols` the template exposes as parameters — see
   `templates/webapi/.template.config/template.json` for the `IncludeTests` boolean
   parameter as an example).
2. Give the template its own **self-contained** `Directory.Build.props` and
   `Directory.Packages.props` — do not let it chain to the repo root's. MSBuild only
   auto-imports the nearest file up the directory tree; a template that accidentally
   inherits the repo's own props would (a) fail to compile once copied out of the repo,
   since the parent props wouldn't exist there, and (b) silently inherit Dorn's own
   analyzer/package versions instead of choosing its own. See `docs/architecture.md` for
   the full rationale.
3. If the template needs code that should stay identical across templates (currently:
   the domain base types and the custom mediator), copy it from `templates/shared/`
   rather than referencing it — templates must not reference files outside their own
   directory tree. Register the new copy pair in
   `eng/scripts/check-shared-sync.sh`'s `PAIRS` array so CI keeps it in sync going
   forward, and see `docs/adr/0008-templates-shared-physical-copy-sync.md`.
4. Add the new template's projects to `Dorn.sln` so `dotnet build Dorn.sln` builds it as
   part of the normal solution build (this is how `templates/webapi` is wired in today).
5. Add a `tests/<Name>Templates.Tests`-style integration test (or extend
   `tests/Templates.Tests`) that generates the template into a temp directory outside the
   repo and runs `dotnet build` against it as a subprocess — this is what actually proves
   the template is self-contained and buildable by an end user, not just inside this
   repo's solution.
6. Wire a new CLI command under `src/Dorn.Cli/Commands/New/` (following
   `NewWebApiCommand`/`NewWebApiSettings`) and register it in `Program.cs`'s `new` branch.
7. Write `docs/templates/<name>.md` documenting what the template generates, following
   `docs/templates/webapi.md`.

## Coding conventions

- **Centrally-managed package versions.** Both `Directory.Packages.props` files in this
  repo (root, and `templates/webapi/Directory.Packages.props`) set
  `ManagePackageVersionsCentrally=true`. Do not add inline `Version="..."` attributes to
  `<PackageReference>` in any `.csproj` — add or update the version in the relevant
  `Directory.Packages.props` instead. The one exception is a transitive-version override,
  which needs both: a `<PackageVersion>` bump in `Directory.Packages.props` *and* a direct
  top-level `<PackageReference Include="..." />` (no version) in the `.csproj` that needs
  the override — central package management only resolves a transitive package to a
  pinned version if something forces NuGet to consider it a direct reference. See the
  `Microsoft.OpenApi` override in `templates/webapi/Directory.Packages.props` and
  `templates/webapi/src/CleanArchWebApi.WebApi/CleanArchWebApi.WebApi.csproj` for a
  worked example (overriding the `Microsoft.OpenApi` version pulled in transitively by
  `Microsoft.AspNetCore.OpenApi` to patch GHSA-v5pm-xwqc-g5wc).
- **No MediatR, FluentAssertions, or Moq.** See ADR 0003 and ADR 0006 for why. Use the
  custom mediator in `templates/shared/Application/Messaging/` for CQRS in templates, and
  xUnit + NSubstitute (plain `Assert.*` — no fluent assertion library) for tests.
- **English only** in code, comments, and docs — Dorn is a community OSS project.

## Verification loop before opening a PR

Run the same checks CI runs, locally, before pushing:

```bash
dotnet build Dorn.sln -c Release
DORN_TEMPLATES_PATH="$(pwd)/templates" dotnet test Dorn.sln
```

If your change touched anything under `templates/shared/` or the corresponding copied
paths in `templates/webapi/` (see `docs/architecture.md`), also run:

```bash
eng/scripts/check-shared-sync.sh
```

All three are enforced in `.github/workflows/ci.yml` on every push and pull request
(build + test run on an `ubuntu-latest`/`windows-latest` matrix; `check-shared-sync.sh`
runs once, on `ubuntu-latest`, since it's a bash script with no OS-specific behavior to
verify).

## License

Dorn is [MIT licensed](../LICENSE). By contributing, you agree your contribution is
licensed under the same terms. See `docs/adr/0007-mit-license.md` for why MIT was chosen.
