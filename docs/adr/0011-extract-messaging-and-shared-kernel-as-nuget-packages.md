# 0011. Extract Messaging and Shared Kernel as NuGet Packages

## Status

Accepted

## Context

ADR 0008 made `templates/shared/` the canonical source for code that needs to be
identical across every Dorn template — the domain base types (`Entity`, `AggregateRoot`,
`INotification`, `Result`) and the custom CQRS mediator (ADR 0003, ADR 0010) — and kept a
physical, byte-for-byte copy of each file inside `templates/webapi/`, enforced by
`eng/scripts/check-shared-sync.sh` as a dedicated CI job. That worked, but it has two
costs that only grow over time:

- **Manual sync discipline.** Every change to a shared file requires a matching edit in
  every template's copy, in the same pull request, or CI fails with a `diff -u`. Nothing
  about the workflow prevents forgetting the second half of that edit before running the
  check locally — the check only detects drift after the fact.
- **It doesn't scale past one template.** `templates/ui` (currently a placeholder, see
  `templates/ui/README.md`) is the next template on the roadmap and will need the same
  domain base types and mediator. Under ADR 0008, adding it means adding a third copy and
  a third `PAIRS` entry to `check-shared-sync.sh` — an *n*-way physical-copy problem that
  gets more expensive, not less, as templates are added.

Real NuGet packages, versioned normally and consumed via ordinary `PackageReference`, are
the standard way to share code across multiple independent consumers — this is exactly
the shape MediatR itself uses (`MediatR.Contracts` + `MediatR`), and Dorn already ships
its own packages elsewhere (`Dorn.Cli` as a future global tool, `Dorn.Templates.WebApi` as
a `dotnet new`-installable template package, ADR 0009). Extracting the shared code the
same way is a natural continuation of a pattern this repo already uses, not a new one.

## Decision

Split the shared code into three packages under a new top-level `packages/` directory
(a sibling of `src/`, `templates/`, `tests/` — see below for why not under `src/`):

- **`Dorn.Messaging.Contracts`** — pure interfaces, zero package dependencies (BCL only):
  `IRequest`/`IRequest<TResponse>`, `IRequestHandler<,>`, `ISender`,
  `IPipelineBehavior<,>` (plus the `RequestHandlerDelegate<TResponse>` delegate), `Unit`,
  `INotification`, `INotificationHandler<>`, `IPublisher`.
- **`Dorn.Messaging`** — the mediator implementation (`Mediator`,
  `ServiceCollectionExtensions.AddMediator`). Depends on `Dorn.Messaging.Contracts` (via
  `ProjectReference`, which `dotnet pack` turns into a package dependency automatically
  since both projects have a `PackageId`) and
  `Microsoft.Extensions.DependencyInjection.Abstractions`.
- **`Dorn.SharedKernel`** — DDD building blocks with no messaging logic: `Entity`,
  `AggregateRoot`, `Result`/`Result<T>`. Depends on `Dorn.Messaging.Contracts` only for
  `INotification`, which `AggregateRoot.DomainEvents` is typed against.

Dependency graph: `Dorn.Messaging.Contracts` ← `Dorn.Messaging`, and
`Dorn.Messaging.Contracts` ← `Dorn.SharedKernel`. Nothing depends on `Dorn.Messaging` or
`Dorn.SharedKernel` except the generated templates themselves.

**Why `INotification` lives in `Dorn.Messaging.Contracts`, not `Dorn.SharedKernel`.**
`INotification` is the wire contract between "something raised an event"
(`AggregateRoot`, a `Dorn.SharedKernel` type) and "something handles it"
(`INotificationHandler<T>`/`IPublisher`, `Dorn.Messaging.Contracts`/`Dorn.Messaging`
types). Both sides need to agree on that one marker type without either package
depending on the other's full surface. Putting `INotification` in `Dorn.SharedKernel`
would force `Dorn.Messaging.Contracts` to depend on `Dorn.SharedKernel` just for one
interface, mixing "DDD building blocks" concerns into "mediator wire contracts" concerns
for no reason. Putting it in `Dorn.Messaging.Contracts` instead, and having
`Dorn.SharedKernel` depend on that lightweight, dependency-free contracts package, is the
cleaner direction — and it's exactly why `MediatR.Contracts` exists separately from
`MediatR` in the library this design already mirrors (ADR 0003).

**Why `packages/` is a new top-level directory, not under `src/`.** `src/` is the `dorn`
CLI tool's own code (`Dorn.Abstractions`, `Dorn.Core`, `Dorn.Cli`) — a scaffolding engine
whose audience is someone running the `dorn` command. `packages/` is code that *generated
projects* depend on at runtime — a completely different audience (an end user's
`TodoItem` aggregate, at runtime, in a project that may never have `dorn` installed).
Nesting `packages/` under `src/` would blur that distinction; keeping it a sibling,
alongside `templates/` and `tests/`, matches how the rest of the repo already separates
"the tool" from "what the tool produces."

`templates/webapi` now references these three packages via ordinary
`<PackageReference>` (versions pinned in `templates/webapi/Directory.Packages.props`,
still its own self-contained file, not chained to the repo root's) instead of keeping a
physical copy of the source. `templates/shared/` and `eng/scripts/check-shared-sync.sh`
are removed entirely — not deprecated in place, removed — because the mechanism they
implemented no longer has anything to check: there is no second copy to drift. ADR 0008
is marked Superseded, not deleted, per this repo's convention of keeping historical
decision records intact.

## Consequences

- **Regression: a generated project is no longer offline-buildable the moment it leaves
  this dev machine.** Before this change, `dorn new webapi`/`dotnet new dorn-webapi`
  produced a project with zero external references — every line of `Entity`,
  `AggregateRoot`, and the mediator lived inside the generated project's own tree. After
  this change, the generated project has three `PackageReference`s that resolve to
  packages not yet published to NuGet.org. A generated project only restores successfully
  today on a machine that has this repo checked out with `./artifacts` populated by
  `eng/scripts/pack-packages.ps1` and the local feed configured (`nuget.config`, or a
  copy of it, or `RestoreAdditionalProjectSources` pointed at the right place). This is a
  real, honest trade-off against ADR 0008's zero-external-dependency guarantee — accepted
  because indefinite copy-paste-and-diff-check doesn't scale past one template, and
  because this is the same "not published yet" gap the `dorn` CLI tool and
  `Dorn.Templates.WebApi` package already have (tracked in `eng/README.md`'s TODO list,
  which now also lists these three packages). Publishing all of them to NuGet.org closes
  this gap; until then, it's a known, documented limitation, not a silent one.
- **Drift is structurally impossible, not just detected.** There is exactly one copy of
  `Entity`/`AggregateRoot`/`Result`/the mediator now — the one inside `packages/` — so
  there is nothing left for `check-shared-sync.sh` to check. `templates/shared/` and
  `check-shared-sync.sh` are gone; the `check-shared-sync` CI job is gone.
  `docs/adr/0008-templates-shared-physical-copy-sync.md` is marked Superseded by this
  ADR, not deleted, preserving the historical record of why the physical-copy approach
  was chosen in the first place.
- **A second template is now cheap.** `templates/ui`, when it's built, adds
  `PackageReference`s to whichever of the three packages it needs — no copy, no new
  `check-shared-sync.sh` `PAIRS` entry, no risk of drift against `templates/webapi`.
  `docs/contributing.md`'s "Adding a new template" section reflects this.
- **`packages/` projects are governed by the repo root's `Directory.Build.props`/
  `Directory.Packages.props`** (net10.0, nullable, MIT license — same as `src/`), unlike
  `templates/webapi`, which deliberately stays self-contained. This is intentional:
  `packages/` projects are real, versioned, shipped libraries, not scaffold source that
  gets renamed and copied out of the repo.

See `eng/README.md` for the new `packages/` layout and `pack-packages.ps1` usage, and
`docs/templates/webapi.md`/`docs/architecture.md` for where the generated project's
`Entity`/`AggregateRoot`/mediator types now come from.
