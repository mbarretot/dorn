# Architecture

## Contents

- [The three `src/` projects](#the-three-src-projects)
- [The custom mediator (ADR 0003)](#the-custom-mediator-adr-0003)
- [Cross-template building blocks: `packages/` (ADR 0010)](#cross-template-building-blocks-packages-adr-0010)
- [Related documents](#related-documents)

Dorn has two halves that are easy to conflate:

- **The CLI tool** (`src/Dorn.Abstractions`, `src/Dorn.Core`, `src/Dorn.Cli`): the
  scaffolding engine, distributed as a `dotnet tool`.
- **The templates** (`templates/`): the project skeletons Dorn generates, each
  self-contained.
- **The packages** (`packages/`): first-party NuGet packages generated projects depend on
  at runtime (the mediator and DDD building blocks). See ADR 0010.

This document covers all three.

## The three `src/` projects

### `Dorn.Abstractions`

Pure contracts, no implementation, no dependency on the Template Engine. Two areas:

- **`Generation`**: `IGenerationEngine` (`ListTemplatesAsync`, `GenerateAsync`), plus the
  records it operates on: `GenerationRequest` (template short name, project name, output
  directory, optional parameters, `Force` flag), `GenerationResult` (success flag, output
  directory, created files, diagnostics), and `GenerationDiagnostic`
  (`Info`/`Warning`/`Error` severity + message).
- **`Templates`**: `ITemplateCatalog` (`GetAvailableTemplatesAsync`,
  `FindByShortNameAsync`) and the `TemplateDescriptor` record it returns (identity, short
  name, name, description, classifications, source path).

Keeping this project dependency-free isolates `Microsoft.TemplateEngine.*` usage inside
`Dorn.Core`: a breaking change in that API surface (it already broke once,
mid-implementation) only requires changing `Dorn.Core`, never the contracts `Dorn.Cli`
codes against.

### `Dorn.Core`

Implements `Dorn.Abstractions` against the embedded Template Engine, and exposes
`AddDornCore(this IServiceCollection)` to register everything as singletons (the
Template Engine environment is expensive to build, safe to share for the process
lifetime).

- **`DornTemplateEngineHost`**: builds an isolated `IEngineEnvironmentSettings` rooted at
  `~/.dorn/template-engine`, deliberately *not* the user's global `~/.templateengine`
  used by `dotnet new`. See ADR 0002.
- **`TemplateLocator`**: resolves the filesystem root of `templates/`, in order: (1)
  `DORN_TEMPLATES_PATH` (dev/tests against a repo checkout), (2) a walk up from
  `AppContext.BaseDirectory` for a `templates/` directory with at least one
  `.template.config` subfolder.
- **`FileSystemTemplateCatalog`**: scans `templates/` directly with
  `Microsoft.TemplateEngine.Edge.Settings.Scanner` rather than "installing" templates
  through `TemplatePackageManager`/`InstallRequest`, since Dorn ships templates as source
  and doesn't need NuGet's package/version/update machinery. Implements `ITemplateCatalog`
  and also exposes the raw `ITemplateInfo` for `TemplateEngineGenerationEngine`.
- **`TemplateEngineGenerationEngine`**: implements `IGenerationEngine` on
  `Microsoft.TemplateEngine.Edge.Template.TemplateCreator.InstantiateAsync`, and enforces
  the `--force` contract itself: the embedded host's default destructive-change handling
  is permissive regardless of `forceCreation`, so without this pre-check `InstantiateAsync`
  would overwrite a non-empty output directory even when told not to.
- **`Validation/ProjectNameValidator`**: checks a proposed project name is valid as both a
  filesystem directory name and the root of a generated C# identifier/namespace (rejects
  invalid path characters, leading digits, reserved Windows device names like
  `CON`/`PRN`/`COM1`).

#### The real embedded Template Engine API (ADR 0002)

The original plan assumed a `Bootstrapper` façade class from older
`Microsoft.TemplateEngine.Edge` docs/samples. **That class does not exist in the version
this repo uses (`10.0.301`, pinned to match the installed .NET 10 SDK).** The real entry
points:

- `Microsoft.TemplateEngine.Edge.EngineEnvironmentSettings`: constructed from a
  `DefaultTemplateEngineHost` plus built-in components from
  `Microsoft.TemplateEngine.Edge.Components` and
  `Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components`.
- `Microsoft.TemplateEngine.Edge.Settings.Scanner`: discovers templates by scanning a
  filesystem path (`ScanAsync`), returning a `ScanResult` whose mount point must stay open
  for the process lifetime (template instantiation reads file contents lazily from it, so
  `FileSystemTemplateCatalog` is a singleton that disposes the scan result on shutdown, not
  per-call).
- `Microsoft.TemplateEngine.Edge.Template.TemplateCreator`: the instantiation entry point,
  via `InstantiateAsync(templateInfo, name, fallbackName, outputPath, inputParameters,
  forceCreation, cancellationToken)`, returning an `ITemplateCreationResult` (status, file
  changes, error message) that `TemplateEngineGenerationEngine` maps onto
  `GenerationResult`.

All three stay wrapped behind `Dorn.Core`'s classes, so a future SDK version narrowing or
renaming this surface only affects `Dorn.Core`: `Dorn.Abstractions` and `Dorn.Cli` never
reference `Microsoft.TemplateEngine.*` directly.

### `Dorn.Cli`

Thin. `Program.cs`:

- Wires a `ServiceCollection` with `AddDornCore()`.
- Adapts it to Spectre.Console.Cli via `Infrastructure/TypeRegistrar` and `TypeResolver`
  (the documented pattern for DI-driven `CommandApp` construction).
- Registers one command branch: `new webapi`, backed by `NewWebApiCommand`.

`NewWebApiCommand` then:

1. Validates the project name via `ProjectNameValidator`.
2. Builds a `GenerationRequest` with the fixed template short name `dorn-webapi`.
3. Calls `IGenerationEngine.GenerateAsync`.
4. Renders the result: a Spectre table of created files plus a "next steps" panel on
   success, or a red diagnostics panel and a non-zero exit code on failure.

## The custom mediator (ADR 0003)

`packages/Dorn.Messaging.Contracts/` and `packages/Dorn.Messaging/` (consumed via ordinary
`PackageReference` by every template that needs CQRS, currently just `webapi`) implement a
MediatR-shaped, independent, MIT-licensed mediator:

- `IRequest<TResponse>` / `IRequest` (the latter is `IRequest<Unit>`, `Unit` a
  zero-information struct for "no return value").
- `IRequestHandler<TRequest, TResponse>.Handle(TRequest, CancellationToken)`.
- `ISender.Send<TResponse>(IRequest<TResponse>, CancellationToken)`.
- `IPipelineBehavior<TRequest, TResponse>.Handle(TRequest, RequestHandlerDelegate<TResponse>, CancellationToken)`
  for decorator-style cross-cutting concerns (validation, logging, transactions).
- `INotificationHandler<TNotification>.Handle(TNotification, CancellationToken)` and
  `IPublisher.Publish(INotification, CancellationToken)` for publish/subscribe:
  zero-or-more handlers per event type, dispatched by `Mediator.Publish`. `INotification`
  lives in `packages/Dorn.Messaging.Contracts/INotification.cs`, so `AggregateRoot`
  (`packages/Dorn.SharedKernel/`) can type its event collection as
  `IReadOnlyCollection<INotification>` while depending only on the dependency-free
  contracts package (ADR 0009, ADR 0010).

All of the above live in `packages/Dorn.Messaging.Contracts/`: pure interfaces, zero
package dependencies, safe to reference from any layer including Domain.

`Mediator : ISender, IPublisher` (`packages/Dorn.Messaging/Mediator.cs`):

- `Send` resolves the handler for a request's concrete type via `IServiceProvider`
  (reflection over `IRequestHandler<,>`), then wraps the call in every registered
  `IPipelineBehavior<,>` for that request/response pair, innermost handler last: the same
  decorator chain MediatR uses, without MediatR's dependency or its RPL-1.5/commercial
  licensing from v13 onward.
- `Publish` resolves every registered `INotificationHandler<,>` for the notification's
  concrete type and invokes each in turn.

`ServiceCollectionExtensions.AddMediator(this IServiceCollection, Assembly)`
(`packages/Dorn.Messaging/`) scans an assembly's concrete classes, registers every
`IRequestHandler<,>`, `IPipelineBehavior<,>`, and `INotificationHandler<>` implementation
found, plus `ISender → Mediator` and `IPublisher → Mediator`.

Further reading: `docs/adr/0003-custom-mediator-instead-of-mediatr.md` (licensing
rationale), `docs/adr/0009-ddd-aggregates-and-domain-events.md` (domain-event dispatch
design), `docs/adr/0010-extract-messaging-and-shared-kernel-as-nuget-packages.md` (why
packages, not copied source), and `docs/templates/webapi.md` (worked examples).

## Cross-template building blocks: `packages/` (ADR 0010)

Code that must stay identical across every template, `Entity`/`AggregateRoot`/`Result`
and `INotification`, plus the entire custom mediator, ships as three NuGet packages under
the top-level `packages/` directory (sibling of `src/`, `templates/`, `tests/`), consumed
via `PackageReference`:

| Package                              | Contents                                                        |
| ------------------------------------- | ----------------------------------------------------------------- |
| `packages/Dorn.Messaging.Contracts/` | Pure mediator interfaces + `INotification`, zero dependencies.  |
| `packages/Dorn.Messaging/`           | The mediator implementation (`Mediator`, `AddMediator`).        |
| `packages/Dorn.SharedKernel/`        | `Entity`, `AggregateRoot`, `Result`/`Result<T>`.                 |

### Why templates can't just project-reference this code

`templates/webapi` must be **self-contained**:

- It ships its own `Directory.Build.props` and `Directory.Packages.props`, not chained to
  the repo root's (MSBuild only auto-imports the nearest file up the tree).
- This keeps the generated project compiling standalone once copied out of the repo, and
  stops it silently inheriting Dorn's own analyzer/package versions. `templates/tests`
  proves this by generating into `Path.GetTempPath()` (outside the repo) and running
  `dotnet build` there as a real subprocess.
- Consequently, `templates/webapi` cannot reference code outside its own directory tree
  via a project reference or `<Compile Include>`: that would break once the template is
  packaged (`eng/scripts/pack-templates.ps1`) or copied out of the repo checkout.

### Why packages instead of a physical copy

This code ships as NuGet packages, not a physical copy per template (the original
approach, ADR 0007, now superseded), because it must stay identical across every template
that needs it.

### Local build

These three packages aren't published to NuGet.org yet:

- Built locally via `eng/scripts/pack-packages.ps1` into `./artifacts`.
- The root `nuget.config`'s `dorn-local` source resolves that folder as a package feed for
  `templates/webapi`'s in-repo build.

See `eng/README.md` and
`docs/adr/0010-extract-messaging-and-shared-kernel-as-nuget-packages.md` for the full
rationale.

## Related documents

- `docs/adr/` (0001 to 0015): the full decision records.
- `docs/templates/webapi.md`: user-facing docs for what `dorn new webapi` generates.
- `docs/contributing.md`: conventions for adding a new template.
