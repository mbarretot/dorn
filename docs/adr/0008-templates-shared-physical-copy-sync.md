# 0008. Sync `templates/shared` → `templates/webapi` by Physical Copy + CI Check

## Status

Superseded by ADR 0011

## Context

Some code needs to be identical across every Dorn template that adopts the same
patterns — currently the domain base types (`Entity`, `AggregateRoot`, `INotification`,
`Result`) and the entire custom CQRS mediator (ADR 0003, ADR 0010: `IRequest`, `ISender`,
`IRequestHandler`, `IPipelineBehavior`, `INotificationHandler`, `IPublisher`, `Mediator`,
`Unit`, and the `AddMediator` registration extension — nine files under
`Application/Messaging/`). Maintaining independent copies of this code per template by
hand, with no enforcement, would let them silently drift apart over time as one template
is edited and the other isn't.

At the same time, `templates/webapi` must be fully self-contained (see
`docs/architecture.md`'s MSBuild note and `tests/Templates.Tests`, which generates into a
temp directory outside the repo and builds it as a standalone project to prove exactly
this): it ships its own non-chaining `Directory.Build.props`/`Directory.Packages.props`,
and — critically for this decision — cannot reference files outside its own directory
tree via a project reference or an MSBuild `<Compile Include>` pointing outside the
template root. That rules out the two most obvious ways to "share" code between
`templates/shared` and `templates/webapi`: a normal project/file reference (breaks
self-containment, and specifically breaks the future goal of packaging `templates/webapi`
as a standalone NuGet template package — packaging cannot follow references outside the
package's own content root), and a symlink (fragile across the Windows/Linux CI matrix
this repo already runs in `.github/workflows/ci.yml`, and not guaranteed to survive every
checkout/archive/zip path a template might travel through before reaching an end user).

## Decision

`templates/shared/` is the canonical source of truth for code meant to be shared across
templates:

- `templates/shared/Domain/Entity.cs`, `templates/shared/Domain/AggregateRoot.cs`,
  `templates/shared/Domain/INotification.cs`, `templates/shared/Domain/Result.cs`
- `templates/shared/Application/Messaging/*.cs` (nine files: `IRequest.cs`, `ISender.cs`,
  `IRequestHandler.cs`, `IPipelineBehavior.cs`, `INotificationHandler.cs`, `IPublisher.cs`,
  `Mediator.cs`, `ServiceCollectionExtensions.cs`, `Unit.cs`)

`templates/webapi` keeps a **physical, byte-for-byte copy** of each file at the
corresponding path:

- `templates/webapi/src/CleanArchWebApi.Domain/Entity.cs`,
  `templates/webapi/src/CleanArchWebApi.Domain/AggregateRoot.cs`,
  `templates/webapi/src/CleanArchWebApi.Domain/INotification.cs`,
  `templates/webapi/src/CleanArchWebApi.Domain/Result.cs`
- `templates/webapi/src/CleanArchWebApi.Application/Messaging/*.cs`

`eng/scripts/check-shared-sync.sh` enforces this: it diffs every file under the canonical
`templates/shared/` locations against its counterpart under `templates/webapi/` and exits
non-zero, printing a `diff -u` for each mismatch, if any file has drifted or a copy is
missing. It runs as a dedicated `check-shared-sync` job in `.github/workflows/ci.yml` on
every push and pull request, on `ubuntu-latest` only (it's a portable bash script with no
OS-specific behavior to verify twice). A future second template that also needs this
shared code would add its own copy-pair entries to the script's `PAIRS` list
(`docs/contributing.md`, step 3 of "Adding a new template").

## Consequences

The physical-copy approach described below was replaced by real NuGet packages once
cross-template code needed to be consumed via ordinary `PackageReference` instead of
copied per-template — see ADR 0011.

- `templates/webapi` stays genuinely self-contained — no reference of any kind reaches
  outside its own directory tree — which keeps `tests/Templates.Tests`'s
  outside-the-repo build honest and keeps the door open for future NuGet template
  packaging (`eng/scripts/pack-templates.ps1`, currently a placeholder).
- Drift between the canonical source and its copy is a CI failure, not a silent bug: a
  contributor who edits `templates/shared/Application/Messaging/Mediator.cs` (or the
  copy under `templates/webapi/`) without updating the other side gets a clear, specific
  `diff -u` failure locally (`eng/scripts/check-shared-sync.sh`) and in CI, rather than
  the two copies quietly diverging until someone notices a bug.
- The cost is manual: there is no MSBuild-level or tooling-level enforcement that a copy
  operation actually happened — `check-shared-sync.sh` only detects drift after the fact
  (locally before a commit, or in CI on push/PR), it does not perform the copy for the
  contributor. `docs/contributing.md` documents the expected workflow (edit
  `templates/shared/`, copy to `templates/webapi/`, run the script) so this doesn't rely
  on institutional memory.
- If `templates/shared/` code diverges in behavior from what a specific template needs in
  the future, this convention offers no branching mechanism — sharing a file means it is
  identical everywhere it's copied. A template needing a genuinely different variant of
  the mediator, for example, would maintain its own copy independently rather than being
  listed as a synced pair.
