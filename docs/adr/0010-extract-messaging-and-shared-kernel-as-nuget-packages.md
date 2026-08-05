# 0010. Extract Messaging and Shared Kernel as NuGet Packages

## Status

Accepted

## Context

ADR 0007 made `templates/shared/` the canonical source for code shared across templates
(domain base types, the CQRS mediator) and kept a physical, byte-for-byte copy inside
`templates/webapi/`, enforced by `eng/scripts/check-shared-sync.sh` in CI. That has two
growing costs: manual sync discipline (every shared-file change needs a matching edit in
every template's copy, or CI fails with a `diff -u`, and the check only detects drift
after the fact), and it doesn't scale past one template (`templates/ui`, the next
template on the roadmap, would mean an *n*-way physical-copy problem).

Real NuGet packages, versioned normally and consumed via `PackageReference`, are the
standard way to share code across multiple independent consumers, the same shape MediatR
itself uses (`MediatR.Contracts` + `MediatR`), and Dorn already ships its own packages
elsewhere (`Dorn.Cli`, `Dorn.Templates.WebApi`, ADR 0008).

## Decision

Split the shared code into three packages under a new top-level `packages/` directory
(a sibling of `src/`, `templates/`, `tests/`):

- **`Dorn.Messaging.Contracts`**: pure interfaces, zero package dependencies (BCL only):
  `IRequest`/`IRequest<TResponse>`, `IRequestHandler<,>`, `ISender`,
  `IPipelineBehavior<,>` (plus `RequestHandlerDelegate<TResponse>`), `Unit`,
  `INotification`, `INotificationHandler<>`, `IPublisher`.
- **`Dorn.Messaging`**: the mediator implementation (`Mediator`,
  `ServiceCollectionExtensions.AddMediator`). Depends on `Dorn.Messaging.Contracts` and
  `Microsoft.Extensions.DependencyInjection.Abstractions`.
- **`Dorn.SharedKernel`**: DDD building blocks with no messaging logic: `Entity`,
  `AggregateRoot`, `Result`/`Result<T>`. Depends on `Dorn.Messaging.Contracts` only for
  `INotification`, which `AggregateRoot.DomainEvents` is typed against.

Dependency graph: `Dorn.Messaging.Contracts` ← `Dorn.Messaging`, and
`Dorn.Messaging.Contracts` ← `Dorn.SharedKernel`. Nothing depends on `Dorn.Messaging` or
`Dorn.SharedKernel` except the generated templates themselves.

**Why `INotification` lives in `Dorn.Messaging.Contracts`, not `Dorn.SharedKernel`**: it's
the wire contract between "something raised an event" (`AggregateRoot`) and "something
handles it" (`INotificationHandler<T>`/`IPublisher`); putting it in `Dorn.SharedKernel`
instead would force `Dorn.Messaging.Contracts` to depend on `Dorn.SharedKernel` for one
interface, mixing DDD concerns into mediator-contract concerns for no reason. This also
mirrors why `MediatR.Contracts` exists separately from `MediatR`.

**Why `packages/` is a new top-level directory, not under `src/`**: `src/` is the `dorn`
CLI tool's own code, run by someone invoking `dorn`; `packages/` is code an end user's
*generated project* depends on at runtime, and may never have `dorn` installed. Nesting
one under the other would blur that audience distinction.

`templates/webapi` now references these three packages via `<PackageReference>` (versions
pinned in `templates/webapi/Directory.Packages.props`, still self-contained, not chained
to the repo root's) instead of a physical copy. `templates/shared/` and
`eng/scripts/check-shared-sync.sh` are removed entirely, since there is no second copy
left to drift. ADR 0007 is marked Superseded, not deleted.

## Consequences

- **Regression: a generated project is no longer offline-buildable the moment it leaves
  this dev machine.** It now has three `PackageReference`s that resolve to packages not
  yet published to NuGet.org; it only restores on a machine with this repo checked out,
  `./artifacts` populated by `eng/scripts/pack-packages.ps1`, and the local feed
  configured. Accepted because indefinite copy-paste-and-diff-check doesn't scale, and
  it's the same "not published yet" gap the `dorn` CLI and `Dorn.Templates.WebApi`
  already have (`eng/README.md`'s TODO list).
- **Drift is structurally impossible, not just detected.** There is exactly one copy of
  `Entity`/`AggregateRoot`/`Result`/the mediator now, so `check-shared-sync.sh`,
  `templates/shared/`, and the `check-shared-sync` CI job are all gone.
- **A second template is now cheap.** `templates/ui` adds `PackageReference`s to whichever
  packages it needs: no copy, no new `PAIRS` entry, no drift risk.
- **`packages/` projects are governed by the repo root's `Directory.Build.props`/
  `Directory.Packages.props`** (net10.0, nullable, MIT license, same as `src/`), unlike
  `templates/webapi`, which deliberately stays self-contained: `packages/` projects are
  real, versioned, shipped libraries, not scaffold source copied out of the repo.

See `eng/README.md` for the `packages/` layout and `pack-packages.ps1` usage.
