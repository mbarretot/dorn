# 0002. Embed Microsoft.TemplateEngine.Edge Instead of Shelling Out to `dotnet new`

## Status

Accepted

## Context

Dorn needs a generation engine to turn `templates/webapi` (and future templates) plus a
project name and parameters into a new project on disk. Two approaches were available:

1. Shell out to the `dotnet new` CLI as a subprocess, passing the template short name and
   arguments, and parse its stdout/exit code.
2. Embed `Microsoft.TemplateEngine.Edge`, `.Abstractions`, and
   `.Orchestrator.RunnableProjects` — the same libraries that power `dotnet new` itself —
   directly inside `Dorn.Core`, and drive template discovery and instantiation in-process.

Shelling out is simpler to write initially, but has real downsides for a tool meant to be
embedded in other workflows: it mutates the invoking user's global `dotnet new` template
cache and settings (`~/.templateengine`) as a side effect of installing/using Dorn's
templates, which pollutes `dotnet new --list` with Dorn-specific templates the user never
asked to see there and makes Dorn's behavior depend on unrelated global state it doesn't
control. It also returns results as unstructured stdout/exit codes rather than a typed
result Dorn can render (a list of created files, structured diagnostics, a success flag).
Finally, it's harder to test — an in-process fake is not possible when the real work
happens in a spawned process.

## Decision

`Dorn.Core` embeds the Template Engine directly rather than shelling out. Concretely:

- `DornTemplateEngineHost` builds an isolated `IEngineEnvironmentSettings` rooted at
  `~/.dorn/template-engine` — never the user's global `~/.templateengine` — so Dorn's
  template cache is fully separate from `dotnet new`'s.
- `FileSystemTemplateCatalog` scans `templates/` directly via
  `Microsoft.TemplateEngine.Edge.Settings.Scanner`, rather than "installing" templates
  through the package-manager machinery (`TemplatePackageManager`/`InstallRequest`) meant
  for versioned NuGet-distributed templates — Dorn ships its templates as source
  alongside the tool.
- `TemplateEngineGenerationEngine` drives instantiation via
  `Microsoft.TemplateEngine.Edge.Template.TemplateCreator.InstantiateAsync`, and maps the
  resulting `ITemplateCreationResult` onto Dorn's own `GenerationResult`/
  `GenerationDiagnostic` types.
- All of this is deliberately isolated behind `IGenerationEngine`/`ITemplateCatalog` in
  `Dorn.Abstractions`, so that `Dorn.Cli` and any future consumer of `Dorn.Core` never
  reference `Microsoft.TemplateEngine.*` directly.

That isolation turned out to matter immediately: the plan for this decision originally
assumed a `Bootstrapper` façade class, based on older `Microsoft.TemplateEngine.Edge`
docs and samples. **That class does not exist in the version actually used here
(`10.0.301`, pinned to match the installed .NET 10 SDK).** The real entry points,
discovered during implementation, are `EngineEnvironmentSettings` (constructed directly),
`Microsoft.TemplateEngine.Edge.Settings.Scanner` for discovery, and
`Microsoft.TemplateEngine.Edge.Template.TemplateCreator` for instantiation. Because all
three were already confined to `Dorn.Core`, adapting to the real API only touched that one
project.

## Consequences

- No mutation of the user's global `dotnet new` state; Dorn's template cache lives at
  `~/.dorn/template-engine`, fully separate.
- Structured, typed results (`GenerationResult`, `GenerationDiagnostic`) instead of
  stdout-parsing, which `Dorn.Cli` renders directly as a Spectre table/panel.
- Testable in-process: `tests/Dorn.Core.Tests` exercises the real engine against a small
  fixture template without spawning a subprocess.
- Accepted risk: the `Microsoft.TemplateEngine.*` public API surface is not guaranteed
  stable across SDK versions — it already changed in a way that broke this project's
  original design assumption (no `Bootstrapper`). Mitigation is architectural, not
  process: all direct usage stays behind `IGenerationEngine`/`ITemplateCatalog`, so a
  future narrowing or renaming again only requires changes inside `Dorn.Core`.
- `Microsoft.TemplateEngine.*` packages must be version-pinned to match the installed SDK
  closely (see ADR 0001) — these are not independently-versioned general-purpose
  libraries, they track the SDK's internal template engine implementation.
