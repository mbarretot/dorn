# Architecture

Dorn has two halves that are easy to conflate but serve different purposes:

- **The CLI tool** (`src/Dorn.Abstractions`, `src/Dorn.Core`, `src/Dorn.Cli`) — the
  scaffolding engine itself, distributed (eventually) as a `dotnet tool`.
- **The templates** (`templates/`) — the actual project skeletons Dorn generates, each an
  independent, self-contained codebase in its own right.

This document covers both, plus the convention that keeps a piece of code shared between
them in sync.

## The three `src/` projects

### `Dorn.Abstractions`

Pure contracts, no implementation, no dependency on the Template Engine. Two areas:

- **`Generation`** — `IGenerationEngine` (`ListTemplatesAsync`, `GenerateAsync`), plus the
  records it operates on: `GenerationRequest` (template short name, project name, output
  directory, optional parameters, `Force` flag), `GenerationResult` (success flag, output
  directory, created files, diagnostics), and `GenerationDiagnostic`
  (`Info`/`Warning`/`Error` severity + message).
- **`Templates`** — `ITemplateCatalog` (`GetAvailableTemplatesAsync`,
  `FindByShortNameAsync`) and the `TemplateDescriptor` record it returns (identity, short
  name, name, description, classifications, source path).

The point of keeping this project dependency-free is isolation: everything that touches
`Microsoft.TemplateEngine.*` directly lives in `Dorn.Core`, so a breaking change in that
API surface (see below — it already broke once, mid-implementation) only requires
changing `Dorn.Core`, never the contracts `Dorn.Cli` codes against.

### `Dorn.Core`

Implements `Dorn.Abstractions` against the embedded Template Engine, and exposes
`AddDornCore(this IServiceCollection)` to register everything as singletons (the
Template Engine environment is expensive to build and safe to share for the process
lifetime).

- **`DornTemplateEngineHost`** — builds an isolated `IEngineEnvironmentSettings` rooted at
  `~/.dorn/template-engine`, deliberately *not* the user's global `~/.templateengine`
  used by `dotnet new`. See ADR 0002 for why Dorn embeds the engine instead of shelling
  out to `dotnet new` in the first place.
- **`TemplateLocator`** — resolves the filesystem root of `templates/`: first
  `DORN_TEMPLATES_PATH` (dev and tests against a repo checkout), then a walk up from
  `AppContext.BaseDirectory` looking for a `templates/` directory containing at least one
  `.template.config` subfolder (a future installed-tool layout; no packaging story exists
  for this yet).
- **`FileSystemTemplateCatalog`** — scans `templates/` directly with
  `Microsoft.TemplateEngine.Edge.Settings.Scanner`, rather than "installing" templates
  through `TemplatePackageManager`/`InstallRequest`. Dorn ships templates as source
  alongside the tool; it doesn't need the package/version/update machinery that exists
  for NuGet-installed `dotnet new` templates. Implements `ITemplateCatalog` and also
  exposes the raw `ITemplateInfo` (not just the `TemplateDescriptor` projection) for
  `TemplateEngineGenerationEngine` to consume.
- **`TemplateEngineGenerationEngine`** — implements `IGenerationEngine` on top of
  `Microsoft.TemplateEngine.Edge.Template.TemplateCreator.InstantiateAsync`. Notably, it
  enforces the `--force` contract itself: the embedded host's default destructive-change
  handling is permissive regardless of `forceCreation`, so without this explicit
  pre-check `InstantiateAsync` would happily overwrite a non-empty output directory even
  when the caller asked it not to.
- **`Validation/ProjectNameValidator`** — checks a proposed project name is valid both as
  a filesystem directory name and as the root of a generated C# identifier/namespace
  (rejects invalid path characters, leading digits, reserved Windows device names like
  `CON`/`PRN`/`COM1`, etc.).

#### The real embedded Template Engine API (ADR 0002)

The original plan assumed a `Bootstrapper` façade class, based on older
`Microsoft.TemplateEngine.Edge` docs/samples. **That class does not exist in the version
this repo actually uses (`10.0.301`, pinned to match the installed .NET 10 SDK exactly).**
This was discovered during implementation, not anticipated in advance. The real entry
points are:

- `Microsoft.TemplateEngine.Edge.EngineEnvironmentSettings` — constructed from a
  `DefaultTemplateEngineHost` plus the built-in components from
  `Microsoft.TemplateEngine.Edge.Components` and
  `Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components`.
- `Microsoft.TemplateEngine.Edge.Settings.Scanner` — discovers templates by scanning a
  filesystem path (`ScanAsync`), returning a `ScanResult` whose mount point must be kept
  open for the process lifetime (template instantiation reads file contents lazily from
  it — this is why `FileSystemTemplateCatalog` is registered as a singleton and disposes
  the scan result on shutdown, not per-call).
- `Microsoft.TemplateEngine.Edge.Template.TemplateCreator` — the actual instantiation
  entry point, via `InstantiateAsync(templateInfo, name, fallbackName, outputPath,
  inputParameters, forceCreation, cancellationToken)`, returning an
  `ITemplateCreationResult` (status, file changes, error message) that
  `TemplateEngineGenerationEngine` maps onto `GenerationResult`.

All three are wrapped behind `Dorn.Core`'s classes precisely so that if a future SDK
version narrows or renames this surface again, the blast radius stays inside
`Dorn.Core` — `Dorn.Abstractions` and `Dorn.Cli` never reference
`Microsoft.TemplateEngine.*` directly.

### `Dorn.Cli`

Thin: `Program.cs` wires a `ServiceCollection` with `AddDornCore()`, adapts it to
Spectre.Console.Cli via `Infrastructure/TypeRegistrar` and `TypeResolver` (the documented
pattern for DI-driven `CommandApp` construction), and registers one command branch —
`new webapi`, backed by `NewWebApiCommand`. The command validates the project name via
`ProjectNameValidator`, builds a `GenerationRequest` with the fixed template short name
`dorn-webapi`, calls `IGenerationEngine.GenerateAsync`, and renders the result as a
Spectre table of created files plus a "next steps" panel on success, or a red diagnostics
panel and a non-zero exit code on failure.

## The custom mediator (ADR 0003)

`templates/shared/Application/Messaging/` (physically copied into every template that
needs CQRS — currently just `webapi`) implements a MediatR-shaped but independent,
MIT-licensed mediator:

- `IRequest<TResponse>` / `IRequest` (the latter is `IRequest<Unit>`, with `Unit` a
  zero-information struct standing in for "no return value").
- `IRequestHandler<TRequest, TResponse>.Handle(TRequest, CancellationToken)`.
- `ISender.Send<TResponse>(IRequest<TResponse>, CancellationToken)`.
- `IPipelineBehavior<TRequest, TResponse>.Handle(TRequest, RequestHandlerDelegate<TResponse>, CancellationToken)`
  for decorator-style cross-cutting concerns (validation, logging, transactions, etc.).

`Mediator : ISender` resolves the handler for a request's concrete type via
`IServiceProvider` (reflection over `IRequestHandler<,>`), then wraps the call in every
registered `IPipelineBehavior<,>` for that request/response pair, innermost handler last,
exactly the decorator chain MediatR itself uses — just without MediatR's dependency or
its RPL-1.5/commercial licensing from v13 onward.
`ServiceCollectionExtensions.AddMediator(this IServiceCollection, Assembly)` scans an
assembly's concrete classes and registers every `IRequestHandler<,>` and
`IPipelineBehavior<,>` implementation it finds, plus `ISender → Mediator` itself.

See `docs/adr/0003-custom-mediator-instead-of-mediatr.md` for the licensing rationale in
full, and `docs/templates/webapi.md` for a worked command+handler example.

## The `templates/shared` → `templates/webapi` sync convention (ADR 0008)

`templates/webapi` must be **self-contained**: it ships its own `Directory.Build.props`
and `Directory.Packages.props` that do *not* chain to the repo root's (MSBuild only
auto-imports the nearest file up the tree, it doesn't merge multiple), specifically so
that (a) the generated project compiles standalone once copied out of the repo, and
(b) it doesn't silently inherit Dorn's own analyzer/package versions, which could mask a
bug that would otherwise be visible to an end user. `tests/Templates.Tests` proves this
by generating into `Path.GetTempPath()` — deliberately outside the repo — and running
`dotnet build` there as a real subprocess.

Self-containment means `templates/webapi` cannot reference code living outside its own
directory tree via a normal project reference or `<Compile Include>` — that would break
the moment the template is packaged (a future NuGet `PackageType=Template` package, see
`eng/scripts/pack-templates.ps1`) or copied out of the repo checkout. But some code —
`BaseEntity`/`Result` in the domain layer, and the entire custom mediator — is meant to
be identical across every template that needs it, not maintained independently per
template. `templates/shared/` is the canonical source for that code:

| Canonical source (`templates/shared/`)         | Physical copy (`templates/webapi/`)                                          |
|--------------------------------------------------|--------------------------------------------------------------------------------|
| `Domain/BaseEntity.cs`, `Domain/Result.cs`        | `src/CleanArchWebApi.Domain/BaseEntity.cs`, `src/CleanArchWebApi.Domain/Result.cs` |
| `Application/Messaging/*.cs` (7 files)            | `src/CleanArchWebApi.Application/Messaging/*.cs`                              |

`eng/scripts/check-shared-sync.sh` diffs every file in the left column against its
counterpart in the right column and fails (non-zero exit, `diff -u` output) if they've
diverged; it runs as a dedicated job in `.github/workflows/ci.yml` on every push and pull
request. See `eng/README.md` and
`docs/adr/0008-templates-shared-physical-copy-sync.md` for the full rationale, including
why a symlink or an MSBuild-level share was rejected.

## Related documents

- `docs/adr/0001-target-framework-net10.md` through
  `docs/adr/0008-templates-shared-physical-copy-sync.md` — the full decision records.
- `docs/templates/webapi.md` — user-facing docs for what `dorn new webapi` generates.
- `docs/contributing.md` — conventions for adding a new template.
