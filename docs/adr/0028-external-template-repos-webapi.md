# 0028. External Template Repositories (Web API)

## Status

Accepted. Extends ADR 0008 and ADR 0027.

## Context

ADR 0027 split the blazor family (`templates/blazor/{wasm,server}`) into `mbarretot/dorn-templates-blazor`,
vendored back into the monorepo at build time. The webapi family is the second to split out, following
the same `dorn-templates-{family}` naming convention, into `mbarretot/dorn-templates-webapi`.

Webapi differs from blazor's starting point in one important way: blazor's source lived only in the
monorepo before its split, so ADR 0027's vendoring mechanism was a genuinely new capability. Webapi is
already published today, packed directly from the in-repo `templates/webapi/` source via
`eng/packaging/Dorn.Templates.WebApi/Dorn.Templates.WebApi.csproj` and `eng/scripts/pack-templates.ps1`,
and released on tag push through `.github/workflows/publish.yml`'s "Pack Dorn.Templates.WebApi" step
(ADR 0008). This split reverses that relationship: `dorn-templates-webapi` becomes the source of truth
and publishes `Dorn.Templates.WebApi` from its own CI; the monorepo becomes a consumer that vendors the
published package back into `templates/webapi/`, the same role the in-repo source used to play.

## Decision

Move `templates/webapi/` source into a new repository, `mbarretot/dorn-templates-webapi`. The new repo
packs and publishes the `PackageType=Template` NuGet package `Dorn.Templates.WebApi` to nuget.org.

Dorn consumes it via the same build-time vendoring mechanism ADR 0027 established: a pinned
`PackageVersion` in the root `Directory.Packages.props`, restored via `PackageDownload` and copied back
into `templates/webapi/` — the exact path the in-repo source occupies today, so `Dorn.Cli.csproj`'s
bundling glob and `DORN_TEMPLATES_PATH` stay unmodified.

The pin starts at `0.0.1`, not a continuation of the monorepo's current `1.0.1` webapi pack version.
This is a deliberate reset: `Dorn.Templates.WebApi`'s version history restarts in the new repository
under its own versioning policy, independent of what the monorepo previously shipped from in-repo
source.

This ADR covers only the additive vendoring mechanism (mirroring ADR 0027's unit B): the pinned pack
restores and its content is proven to land at `templates/webapi`, while the existing
`eng/packaging/Dorn.Templates.WebApi/*` pack-from-source path and `eng/scripts/pack-templates.ps1`
keep working unchanged, side by side. Deleting the in-repo source and retiring the pack-from-source
path — the unit C equivalent of ADR 0027's follow-up — is deliberately deferred to a later change,
gated on confirming the vendored package actually restores once `dorn-templates-webapi` has a real
nuget.org publish.

## Consequences

- `dorn new webapi` stays fully offline at generate time — vendoring happens at dorn build/CI time,
  never at end-user generate time.
- Dorn's own release process gains the same new failure mode ADR 0027 introduced for blazor: if the
  pinned webapi pack is unpublished, yanked, or nuget.org is unreachable, dorn's build and release fail
  until the pin resolves.
- Until the deferred follow-up lands, webapi is published from two places at once: the monorepo's
  existing pack-from-source path (still load-bearing) and the new repo's not-yet-published `0.0.1`.
  Nothing in this monorepo consumes the vendored copy yet.
- A webapi bug fix will eventually span two pull requests in two repositories (fix template, publish
  pack, then bump dorn's pin), the same tradeoff ADR 0027 accepted for blazor.
- Every dorn contributor gains one more mandatory step before testing: `vendor-webapi-templates.ps1`.

## Alternatives

Same alternatives ADR 0027 considered and rejected apply unchanged (git submodule, bespoke GitHub
release fetch, `dotnet new install` at generate time, a general multi-root template abstraction now).
No new alternative surfaced specific to webapi.

## Related

- [ADR 0008: Dual-distribution `dotnet new` template pack](./0008-dual-distribution-dotnet-new-template-pack.md)
- [ADR 0010: Extract Messaging and Shared Kernel as NuGet packages](./0010-extract-messaging-and-shared-kernel-as-nuget-packages.md)
- [ADR 0020: NuGet Trusted Publishing and Test-Gated Releases](./0020-nuget-trusted-publishing-and-test-gated-releases.md)
- [ADR 0026: GitVersion for package versioning](./0026-gitversion-for-package-versioning.md)
- [ADR 0027: External template repositories (blazor first)](./0027-external-template-repos-blazor-first.md)
