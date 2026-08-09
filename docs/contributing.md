# Contributing

Thanks for considering a contribution to Dorn. This document covers adding a new
template, coding conventions, the pre-PR verification loop, PR title/description
format, and licensing.

## Adding a new template

| Template | Role | Notes |
| -------- | ---- | ----- |
| `webapi` | Reference implementation | Follow its pattern for a new template. |
| `grpc` | Second, narrower worked example (`templates/grpc/`, [`docs/templates/grpc.md`](./templates/grpc.md)) | Same seven steps, fixed to one combination instead of `webapi`'s `--database`/`--orm`/`--orchestrator` choices. |
| `worker` | Third worked example, non-transport trigger (`templates/worker/`, [`docs/templates/worker.md`](./templates/worker.md)) | Same seven steps, fixed scope like `grpc`; the presentation layer is a `PeriodicTimer`-driven `BackgroundService` instead of an inbound request. |
| `ui` | Next on the roadmap | Currently just a placeholder at `templates/ui/README.md`. |

Steps to add a new template:

1. Create `templates/<name>/` with `.template.config/template.json` (identity,
   `shortName`, `sourceName`, any `symbols`); see `templates/webapi/.template.config/template.json`'s
   `IncludeTests` boolean for an example.
2. Give the template its own self-contained `Directory.Build.props`/`Directory.Packages.props`;
   do not chain to the repo root's (MSBuild only auto-imports the nearest file up the tree).
   Inheriting the repo's props breaks compilation once copied out of the repo and silently
   pulls in Dorn's own analyzer/package versions. See `docs/architecture.md`.
3. If the template needs code shared across templates (currently the domain base types
   and the custom mediator), add a `PackageReference` to
   `Dorn.SharedKernel`/`Dorn.Messaging.Contracts`/`Dorn.Messaging` (pinned in the
   template's own `Directory.Packages.props`), no copying required. See
   `docs/adr/0010-extract-messaging-and-shared-kernel-as-nuget-packages.md`.
4. Add the new template's projects to `Dorn.slnx` so `dotnet build Dorn.slnx` builds it
   (how `templates/webapi` is wired in today).
5. Add a `templates/tests/<Name>TemplateGenerationTests.cs`-style integration test (or
   extend `templates/tests`) that generates into a temp directory outside the repo and
   runs `dotnet build` against it as a subprocess, proving the template is self-contained
   and buildable by an end user.
6. Wire a new CLI command under `src/Dorn.Cli/Commands/New/` (following
   `NewWebApiCommand`/`NewWebApiSettings`) and register it in `Program.cs`'s `new` branch.
7. Write `docs/templates/<name>.md` documenting what the template generates, following
   `docs/templates/webapi.md`.

## Coding conventions

- **Centrally-managed package versions.** Both `Directory.Packages.props` files (root,
  and `templates/webapi/Directory.Packages.props`) set `ManagePackageVersionsCentrally=true`.
  - **Rule:** no inline `Version="..."` on `<PackageReference>` in any `.csproj`; set it
    in the relevant `Directory.Packages.props`.
  - **Exception:** a transitive-version override needs both a `<PackageVersion>` bump in
    `Directory.Packages.props` and a direct top-level `<PackageReference Include="..." />`
    (no version) in the `.csproj`: central package management only pins a transitive
    package once something forces NuGet to treat it as direct.
  - **Example:** `templates/webapi/Directory.Packages.props` and
    `templates/webapi/src/CleanArchWebApi.WebApi/CleanArchWebApi.WebApi.csproj` override
    `Microsoft.OpenApi`'s transitive version from `Microsoft.AspNetCore.OpenApi` to patch
    GHSA-v5pm-xwqc-g5wc.
- **No MediatR, FluentAssertions, or Moq** (ADR 0003, ADR 0006). Use the custom mediator
  (`Dorn.Messaging.Contracts` + `Dorn.Messaging`, ADR 0010) for CQRS, and xUnit +
  NSubstitute (plain `Assert.*`) for tests.
- **English only** in code, comments, and docs: Dorn is a community OSS project.

## Verification loop before opening a PR

Run the same checks CI runs, locally, before pushing:

```bash
pwsh eng/scripts/pack-packages.ps1
dotnet build Dorn.slnx -c Release
DORN_TEMPLATES_PATH="$(pwd)/templates" DORN_LOCAL_NUGET_FEED="$(pwd)/artifacts" dotnet test Dorn.slnx
```

`pack-packages.ps1` must run first: `templates/webapi` and `templates/tests` both resolve
`Dorn.Messaging.Contracts`/`Dorn.Messaging`/`Dorn.SharedKernel` from the local
`./artifacts` feed (`templates/tests` via `DORN_LOCAL_NUGET_FEED`; see
`docs/adr/0010-extract-messaging-and-shared-kernel-as-nuget-packages.md`).

Enforced in `.github/workflows/ci.yml` on every push/PR (`ubuntu-latest`/`windows-latest`
matrix).

## Pull request title and description

**Title**: an emoji matching the change type, then a conventional-commit-style summary.

| Emoji | Type | Example |
|---|---|---|
| ✨ | `feat` | `✨ feat: opt-in JWT auth for the webapi template` |
| 🐛 | `fix` | `🐛 fix: audience validation missing on azure-ad tokens` |
| 📚 | `docs` | `📚 docs: ADR 0017 + observability template reference` |
| ♻️ | `refactor` | `♻️ refactor: comment cleanup round 7` |
| 🔀 | `merge` | `🔀 merge: develop → main` |

**Description**: sections with emoji headers, tables for structured info, bullets/checklists
for the rest. Not running prose.

```markdown
## 🎯 What & Why
1-2 sentences: what changed, why it was needed.

## 📦 What's Included
| Area | Change |
|---|---|
| `AddAzureAdAuth` | Real Microsoft.Identity.Web, not hand-rolled |

## ✅ Verification
- [x] `dotnet build` → 0 errors
- [x] 44/44 TemplateGenerationTests
- [x] `csharpier check` clean

## 📊 Stats
| Metric | Value |
|---|---|
| Files | 7 |
| Lines | +234 / -9 |
```

Scale sections to the change: a one-file docs fix doesn't need a Stats table, a multi-service
feature does.

## License

Dorn is [MIT licensed](../LICENSE). By contributing, you agree your contribution is
licensed under the same terms.
