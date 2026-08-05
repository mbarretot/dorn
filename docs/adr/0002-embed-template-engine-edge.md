# 0002. Embed Microsoft.TemplateEngine.Edge Instead of Shelling Out to `dotnet new`

## Status

Accepted

## Context

Dorn needs a generation engine to turn `templates/webapi` plus a project name and
parameters into a new project on disk: either shell out to the `dotnet new` CLI and
parse its stdout/exit code, or embed `Microsoft.TemplateEngine.Edge`, `.Abstractions`,
and `.Orchestrator.RunnableProjects` (the libraries that power `dotnet new` itself)
directly inside `Dorn.Core`.

Shelling out is simpler initially but has real downsides for a tool meant to be embedded
elsewhere:

- **Mutates global state**: changes the user's global `dotnet new` cache
  (`~/.templateengine`).
- **Pollutes discovery**: adds Dorn's templates to `dotnet new --list` unasked.
- **Untyped results**: unstructured stdout/exit codes rather than a typed result.
- **Harder to test**: an in-process fake isn't possible when work happens in a spawned
  process.

## Decision

`Dorn.Core` embeds the Template Engine directly rather than shelling out:

- `DornTemplateEngineHost` builds an isolated `IEngineEnvironmentSettings` rooted at
  `~/.dorn/template-engine`, never the user's global `~/.templateengine`.
- `FileSystemTemplateCatalog` scans `templates/` directly via
  `Microsoft.TemplateEngine.Edge.Settings.Scanner`, rather than "installing" templates
  through the package-manager machinery meant for versioned NuGet-distributed templates:
  Dorn ships its templates as source alongside the tool.
- `TemplateEngineGenerationEngine` drives instantiation via
  `Microsoft.TemplateEngine.Edge.Template.TemplateCreator.InstantiateAsync`, mapping the
  resulting `ITemplateCreationResult` onto Dorn's own `GenerationResult`/
  `GenerationDiagnostic` types.
- All of this is isolated behind `IGenerationEngine`/`ITemplateCatalog` in
  `Dorn.Abstractions`, so `Dorn.Cli` and other consumers never reference
  `Microsoft.TemplateEngine.*` directly.

That isolation mattered immediately: the plan assumed a `Bootstrapper` façade class per
older docs, but **that class does not exist** in the version actually used (`10.0.301`).
The real entry points, `EngineEnvironmentSettings` (constructed directly), `Scanner`, and
`TemplateCreator`, were already confined to `Dorn.Core`, so adapting to the real API only
touched that one project.

## Consequences

- No mutation of the user's global `dotnet new` state; Dorn's template cache lives at
  `~/.dorn/template-engine`, fully separate.
- Structured, typed results instead of stdout-parsing, rendered directly as a Spectre
  table/panel.
- Testable in-process: `tests/Dorn.Core.Tests` exercises the real engine against a small
  fixture template without spawning a subprocess.
- **Accepted risk**: the `Microsoft.TemplateEngine.*` public API isn't guaranteed stable
  across SDK versions; it already broke once. All usage stays behind
  `IGenerationEngine`/`ITemplateCatalog`, so a future break again only touches
  `Dorn.Core`.
- `Microsoft.TemplateEngine.*` packages must stay version-pinned to match the installed
  SDK (see ADR 0001); they track the SDK's internal implementation, not independent
  versioning.
