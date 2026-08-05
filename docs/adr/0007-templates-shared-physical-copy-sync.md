# 0007. Sync `templates/shared` → `templates/webapi` by Physical Copy + CI Check

## Status

Superseded by ADR 0010

## Context

Some code needs to be identical across every Dorn template adopting the same patterns:
domain base types (`Entity`, `AggregateRoot`, `INotification`, `Result`) and the entire
custom CQRS mediator (ADR 0003, ADR 0009: `IRequest`, `ISender`, `IRequestHandler`,
`IPipelineBehavior`, `INotificationHandler`, `IPublisher`, `Mediator`, `Unit`,
`AddMediator`, nine files under `Application/Messaging/`). Maintaining independent copies
by hand, with no enforcement, would let them silently drift.

`templates/webapi` must also be fully self-contained (see `docs/architecture.md` and
`templates/tests`, which builds it standalone outside the repo). It ships its own
non-chaining `Directory.Build.props`/`Directory.Packages.props` and cannot reference
files outside its own directory tree. That rules out a normal project/file reference
(breaks self-containment and future NuGet template packaging) and a symlink (fragile
across the Windows/Linux CI matrix and not guaranteed to survive checkout/archive/zip
paths).

## Decision

`templates/shared/` is the canonical source of truth:

- `templates/shared/Domain/Entity.cs`, `AggregateRoot.cs`, `INotification.cs`,
  `Result.cs`
- `templates/shared/Application/Messaging/*.cs` (nine files: `IRequest.cs`, `ISender.cs`,
  `IRequestHandler.cs`, `IPipelineBehavior.cs`, `INotificationHandler.cs`,
  `IPublisher.cs`, `Mediator.cs`, `ServiceCollectionExtensions.cs`, `Unit.cs`)

`templates/webapi` keeps a **physical, byte-for-byte copy** of each file at the
corresponding path under `templates/webapi/src/CleanArchWebApi.Domain/` and
`.../CleanArchWebApi.Application/Messaging/`.

`eng/scripts/check-shared-sync.sh` diffs every canonical file against its
`templates/webapi/` counterpart and exits non-zero (printing a `diff -u`) on drift or a
missing copy. Runs as a dedicated `check-shared-sync` job in `.github/workflows/ci.yml`
on every push and PR, `ubuntu-latest` only. A future second template would add its own
copy-pair entries to the script's `PAIRS` list (`docs/contributing.md`, step 3).

## Consequences

This physical-copy approach was later replaced by real NuGet packages consumed via
`PackageReference`; see ADR 0010.

- `templates/webapi` stays genuinely self-contained, keeping the door open for future
  NuGet template packaging (`eng/scripts/pack-templates.ps1`).
- **Drift is a CI failure, not a silent bug**: an edit to one copy without the other gets
  a clear `diff -u` failure locally and in CI.
- **The cost is manual**: nothing enforces the copy actually happened;
  `check-shared-sync.sh` only detects drift after the fact.
  `docs/contributing.md` documents the expected workflow.
- This convention offers no branching mechanism: a template needing a genuinely
  different variant of shared code would maintain its own copy independently rather than
  being listed as a synced pair.
