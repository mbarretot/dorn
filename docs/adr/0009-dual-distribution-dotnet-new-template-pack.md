# 0009. Dual Distribution: Standalone `dotnet new` Template Package

## Status

Accepted

## Context

`templates/webapi` was always designed to be spec-compliant with the .NET Template
Engine (`.template.config/template.json`, identity `Dorn.Templates.WebApi`, short name
`dorn-webapi`) and, per ADR 0008, fully self-contained — no reference of any kind reaches
outside its own directory tree. That self-containment was explicitly justified in ADR
0008 as "keeps the door open for future NuGet template packaging
(`eng/scripts/pack-templates.ps1`, currently a placeholder)".

Despite that groundwork, the original implementation plan
(`quiero-crear-un-repositorio-curried-dragon.md`, "Decisiones tomadas por defecto")
explicitly deferred actually building the packaging step:

> **Distribución dual** (`dotnet new install` directo sin pasar por `dorn`): queda
> técnicamente habilitada porque `template.json` ya es spec-compliant, pero fuera de
> alcance documentado para v1.

`eng/scripts/pack-templates.ps1` accordingly shipped as a stub (`exit 1` plus a TODO
comment describing the expected shape), and the `dorn` CLI's own embedded
`Microsoft.TemplateEngine.Edge` host (ADR 0002, isolated under `~/.dorn/template-engine`)
was the only supported way to generate a project from `templates/webapi`.

The user later clarified that a standalone, vanilla-`dotnet new`-installable template
package is not a nice-to-have follow-up — it's a core v1 goal. Two reasons drove this:

1. `dotnet new install Dorn.Templates.WebApi` must work independently of ever installing
   or running the `dorn` CLI tool, for users who only want the template and not a new CLI
   to learn.
2. The exact same `<PackageType>Template</PackageType>` NuGet mechanism that enables
   `dotnet new install` is what Visual Studio's "Create a new project" search uses to
   discover third-party project templates. Without a real NuGet package, `templates/webapi`
   is invisible to that surface entirely, regardless of the `dorn` CLI's own capabilities.

This ADR documents reversing that one specific deferral. Every other decision in the
original plan and in ADRs 0001–0008 stands unchanged — in particular, ADR 0002's decision
to embed the Template Engine in the `dorn` CLI (rather than shelling out to `dotnet new`)
and ADR 0008's shared-file-sync mechanism are both prerequisites this ADR builds on top
of, not decisions it revisits.

## Decision

`templates/webapi` is now distributed through two fully independent channels that both
point at the exact same template content:

1. **`dorn new webapi`** — the existing `dorn` CLI path (ADR 0002), using an isolated
   Template Engine host under `~/.dorn/template-engine`, separate from the global
   `~/.templateengine` cache that `dotnet new install`/`uninstall` manipulate. Unchanged
   by this ADR.
2. **`dotnet new install Dorn.Templates.WebApi`** (new) — a standard NuGet package with
   `<PackageType>Template</PackageType>`, installable and usable with vanilla `dotnet new`,
   with no dependency on the `dorn` CLI, and discoverable in Visual Studio's "Create a new
   project" search via the same package-type mechanism.

The packaging project lives at `eng/packaging/Dorn.Templates.WebApi/Dorn.Templates.WebApi.csproj`
— **outside** `templates/webapi/` — rather than inside it. This is deliberate: NuGet's
content-packing glob (`Content Include="../../../templates/webapi/**/*"`) only reaches
into the template directory to *read* files for packaging; the packaging `.csproj` itself
never becomes part of `templates/webapi`'s own tree. If the packaging project lived inside
`templates/webapi/`, it would get picked up and instantiated into every user's generated
project by the Template Engine (which copies everything under the template root except
what `template.json` explicitly excludes) — an unwanted, confusing artifact bundled into
every scaffolded solution. Keeping packaging plumbing in `eng/` (already the convention
for repository-maintenance tooling that must not ship to end users — see `eng/README.md`)
avoids that entirely.

`eng/scripts/pack-templates.ps1` is now a real script (was: a stub) that:

1. Runs `eng/scripts/check-shared-sync.sh` first and fails fast on drift — a stale
   physical copy of `templates/shared/` code (ADR 0008) must never ship inside the NuGet
   package.
2. Runs `dotnet pack` against the `eng/packaging/Dorn.Templates.WebApi` project, versioned
   via an optional `-Version` parameter (default `0.1.0-dev`), emitting to `./artifacts`.

`.github/workflows/ci.yml` runs this script and a full install → list → generate → build →
uninstall smoke cycle on `ubuntu-latest`, so a regression in either distribution channel
fails CI loudly.

Publishing `Dorn.Templates.WebApi` to NuGet.org is **not** part of this change — same
deferred status as the `dorn` CLI's own NuGet publishing (see the `TODO(eng)` release/publish
job marker in `.github/workflows/ci.yml` and `eng/README.md`). Until that exists,
`dotnet new install Dorn.Templates.WebApi` is documented as installing from a local
`./artifacts/*.nupkg` path built by `pack-templates.ps1`.

## Consequences

- Two distribution channels now need to be kept working, but both are thin plumbing over
  the same `templates/webapi/` content — there is no content fork and therefore no drift
  risk between them. The NuGet package is generated by packing `templates/webapi/` as-is;
  it does not maintain a second copy of any template file. The only artifact that could
  drift is the packaging `.csproj`'s include/exclude glob itself, which is a small,
  reviewable surface.
- `templates/webapi` must remain self-contained for both channels to keep working (ADR
  0008 already established and enforces this via `check-shared-sync.sh`); this ADR adds
  no new constraint on that front, it exercises a constraint that already existed.
- CI now smoke-tests both paths: the existing `dorn new webapi` path (via
  `tests/Templates.Tests`, unaffected by this change) and the new
  `dotnet new install`/`dotnet new dorn-webapi`/`dotnet new uninstall` path (new CI step
  in `build-and-test`, ubuntu-only).
- Visual Studio discoverability is a direct, no-extra-work consequence of using
  `<PackageType>Template</PackageType>` — no separate VS-specific packaging step is
  needed, but this also means VS discoverability is gated on the same not-yet-implemented
  NuGet.org publish step as `dotnet new install Dorn.Templates.WebApi` by package ID.
- Local verification (this change): `pwsh eng/scripts/pack-templates.ps1` produces
  `./artifacts/Dorn.Templates.WebApi.0.1.0-dev.nupkg`; installing it, generating a project
  with `dotnet new dorn-webapi`, building it, and exercising the real EF Core
  auto-migrate-on-startup path with a `POST /api/todos` request all succeeded end-to-end;
  uninstalling it cleanly removed `dorn-webapi` from the global template cache; and the
  `dorn` CLI's own isolated-host path (`dorn new webapi` via `DORN_TEMPLATES_PATH`)
  continued to work unaffected throughout, confirming the two caches (`~/.dorn/template-engine`
  vs. the global `~/.templateengine`) are genuinely independent.
