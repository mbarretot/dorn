# 0027. External Template Repositories (Blazor First)

## Status

Accepted. Extends ADR 0008; supersedes in part the 2026-08-10 deferral.

## Context

Dorn's four template families (`webapi`, `grpc`, `worker`, `blazor`) live in one monorepo, share
one `Dorn.slnx` CI pass, and release under one `v*` tag. The blazor family alone contributes two
self-contained template trees, three generation-test files, and 13 `Dorn.slnx` entries, none of
which any other family's contributors need to build or wait on.

On 2026-08-10 (Engram `sdd/dorn-feature-backlog/explore` obs #4) this exact split was considered
and explicitly deferred, on the reasoning "defer until coupling actually hurts." That trigger has
now fired: blazor's CI and test footprint is large enough, and independent enough of the other
three families, that every unrelated template change pays its build/test cost. This ADR is that
trigger condition resolving, not a reversal of the earlier judgment — the earlier decision was
correct given the information available at the time.

## Decision

Move `templates/blazor/{wasm,server}` source into a new repository, `mbarretot/dorn-templates-blazor`,
following the `dorn-templates-{family}` naming convention for any future family that splits out the
same way. The new repo packs and publishes two `PackageType=Template` NuGet packages
(`Dorn.Templates.BlazorWasm`, `Dorn.Templates.BlazorServer`) to nuget.org.

Dorn consumes them via build-time vendoring (b1): a pinned `PackageVersion` per pack in the root
`Directory.Packages.props`, restored via `PackageDownload` and copied back into
`templates/blazor/{wasm,server}` — the exact path the deleted sources occupied, so `Dorn.Cli.csproj`'s
existing bundling glob and `DORN_TEMPLATES_PATH` stay unmodified. `Dorn.Cli` and `Dorn.Core` receive
zero changes. The pin lives in `Directory.Packages.props` specifically because Dependabot already
discovers and bumps `PackageVersion` entries there — no bespoke pin file.

The new repo versions its packs from the pushed `v*` tag directly (`-p:PackageVersion=`), not
GitVersion. ADR 0026 adopted GitVersion to eliminate a hand-maintained default drifting from
reality across four packages sharing one script; the new repo has one version source (the tag)
and two artifacts with no self-consuming pin, so that problem does not exist there and GitVersion
would add machinery with no offsetting benefit.

## Consequences

- `dorn new blazor-*` stays fully offline at generate time — vendoring happens at dorn build/CI
  time, never at end-user generate time.
- Dorn's own release process gains a new failure mode: if the pinned pack is unpublished, yanked,
  or nuget.org is unreachable, dorn's build and release fail until the pin resolves.
- A blazor bug fix now spans two pull requests in two repositories (fix template, publish pack,
  then bump dorn's pin) instead of one.
- Every dorn contributor gains one mandatory step before testing: `vendor-blazor-templates.ps1`.
- Two cross-repo drift seams now exist. `ThemeValidator.ValidThemes` is guarded by
  `VendoredBlazorTemplateTests` (added in the follow-up unit that deletes the in-repo sources) —
  its counterpart, `template.json`'s `Theme` choices, now lives in the external repo.
  `DoctorCommand.TailwindVersion` (`src/Dorn.Cli/Commands/Doctor/DoctorCommand.cs:21-22`) carries a
  "keep in sync with `templates/blazor/wasm/build/Tailwind.targets`" comment that stays accurate
  only while that path is still vendored in-repo; the follow-up unit that deletes the in-repo
  sources also updates this comment to name the external repo explicitly, with no automated guard.

## Alternatives

- **Git submodule**: rejected. Keeps CI, review, and release coupled to a SHA pin instead of a
  semver pin, and submodule ergonomics (detached-HEAD checkouts, easy-to-forget `--recurse`) add
  contributor friction with no packaging benefit over NuGet.
- **Bespoke GitHub release fetch at generate time**: rejected. Reinvents NuGet's checksum,
  versioning, and local-cache behavior with no ADR precedent in this repo, for no capability NuGet
  does not already provide.
- **b2, `dotnet new install` at generate time**: deferred, not rejected outright. Needs an
  `ITemplateSourceProvider`-shaped abstraction in `Dorn.Core` that does not exist yet, and would
  make `dorn new blazor-*` require network access at generate time — a regression from today's
  fully offline behavior.
- **A `Dorn.Core` multi-root template abstraction now**: rejected. Consistent with ADR 0007's
  extraction pattern (generalize only once a second real consumer exists): blazor is the first
  family to split out, so a general mechanism has no second data point yet.

## Related

- [ADR 0008: Dual-distribution `dotnet new` template pack](./0008-dual-distribution-dotnet-new-template-pack.md)
- [ADR 0010: Extract Messaging and Shared Kernel as NuGet packages](./0010-extract-messaging-and-shared-kernel-as-nuget-packages.md)
- [ADR 0020: NuGet Trusted Publishing and Test-Gated Releases](./0020-nuget-trusted-publishing-and-test-gated-releases.md)
- [ADR 0022: Copy-owned UI components](./0022-copy-owned-ui-components.md)
- [ADR 0023: Blazor WASM scoped MVP](./0023-blazor-wasm-scoped-mvp.md)
- [ADR 0024: Blazor Server template scoped MVP](./0024-blazor-server-scoped-mvp.md)
- [ADR 0025: Extract Dorn.WebUI.Primitives as a NuGet package](./0025-extract-dorn-webui-primitives-as-nuget-package.md)
- [ADR 0026: GitVersion for package versioning](./0026-gitversion-for-package-versioning.md)
