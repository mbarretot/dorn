# 0026. GitVersion for Package Versioning

## Status

Accepted

## Context

`eng/scripts/pack-packages.ps1` packed the four `packages/*.csproj` libraries with a hand-maintained default version (`$Version = "1.0.1"`, with `Dorn.WebUI.Primitives` given its own ad hoc override parameter after a real incident: the script's default drifted out of sync with the package's actual content, and GitHub Actions' NuGet cache served the stale extraction under the unchanged version number). Every package sharing one hardcoded default, remembered and bumped by hand, was the root cause — not a one-off mistake.

`publish.yml` already derives the *published* version correctly, from the pushed `vX.Y.Z` tag (`GITHUB_REF_NAME#v`) — that part needed no change to its trigger.

## Decision

Adopt [GitVersion](https://gitversion.net/) (`GitVersion.MsBuild`) to compute `packages/*.csproj`'s version from git tags automatically, and delete `pack-packages.ps1` — both call sites now run plain `dotnet pack`.

Two other tools were evaluated and rejected on hands-on testing — see Alternatives; both rejections are backed by actually running `dotnet pack` against this repo, not documentation alone.

| Concern | Policy |
| --- | --- |
| Version source | An exact `v*` tag on the commit being built is used as-is (verified: tagging HEAD `v9.9.9` packs `9.9.9`) — never hand-typed per release |
| Config | None required — zero-config, no `version.json`/`GitVersion.yml` needed for this repo's linear, single-tag model |
| Real release (`publish.yml`) | Trigger stays `push: tags: v*`, unchanged. `dotnet pack`, no override — GitVersion reads the exact pushed tag directly |
| Local dev/CI feed (`build-test.yml`) | Each `dotnet pack` passes `-p:DisableGitVersionTask=true -p:PackageVersion=<pinned>` explicitly, reading `<pinned>` from the checked-in `templates/*/Directory.Packages.props` |
| Checkout | `actions/checkout` needs `fetch-depth: 0` in both workflows — GitVersion needs real tag/commit history, not the default shallow clone |

The local-feed override is required, not incidental: templates' `Directory.Packages.props` pins are end-user-facing product decisions (*what version does a freshly-generated project reference*), not CI plumbing — they must stay exact, human-chosen values, and Central Package Management has no floating-version support to make them track a moving target automatically ([NuGet/Home#10432](https://github.com/NuGet/Home/issues/10432), open). The pinned value itself is never invented: it always equals either a real historical release tag, or — when a package has diverged locally and not yet been released — a value chosen by the maintainer at the point they made that change, exactly as `Directory.Packages.props` pins already work for every other dependency in this repo.

An earlier version of this mechanism used a **local-only tag** (`git tag -f "v<pinned>"` on HEAD, deleted right after pack) instead of an explicit override, on the theory that GitVersion reading an exact HEAD tag is already proven (see the `v9.9.9` test above). That broke specifically under GitHub Actions' `pull_request` trigger: the checkout is a synthetic `pull/<N>/merge` ref, and GitVersion's branch-name heuristics compute a `PullRequest<N>`-suffixed prerelease version for that ref regardless of a tag on HEAD, silently ignoring the local tag. `dotnet pack` then produced a version that never matched the pin, so `dotnet restore` fell through `nuget.config`'s `nuget.org` source and resolved the *real* published version instead of the freshly-packed one — surfacing as missing-type build errors for any type added since that real release (found via an actual PR CI run, not inferred). `-p:PackageVersion=` alone doesn't fix this either: `GitVersion.MsBuild`'s own target unconditionally overwrites `PackageVersion` after computing it (verified by reading `GitVersion.MsBuild.targets`), clobbering a command-line override. `DisableGitVersionTask=true` skips that target entirely, so the explicit `PackageVersion` stands.

## Consequences

- No version is ever hand-typed for `packages/*.csproj` packing again; the previous "script default silently drifts from reality" failure mode is now structurally impossible.
- Cutting a release is unchanged: `git tag vX.Y.Z && git push --tags`; `publish.yml`'s trigger and ADR 0020's tag-ownership/version-derivation policy are untouched.
- Bumping a template's pinned dependency version is still a deliberate, manual edit to that template's `Directory.Packages.props` — unchanged, and correctly so, since it is a product decision.
- Both workflows now do a full (`fetch-depth: 0`) checkout instead of a shallow one — slightly slower checkout, required for any git-tag-based versioning tool.
- `eng/scripts/pack-packages.ps1` is gone; both workflows call `dotnet pack` directly per package.

## Alternatives (both hands-on tested, both rejected)

- **Nerdbank.GitVersioning:** `dotnet`-org pedigree (strongest of the three), but empirically confirmed incompatible with this repo's need: its computed version comes from `version.json`'s own `"version"` field + git height, never from an actual tag's digits — even tagging HEAD with an arbitrary exact version (`v9.9.9`) did not make it pack `9.9.9`; the tag only toggled prerelease-vs-public formatting. Its documented `NBGV_Disabled` MSBuild property, expected to allow a manual override, does not exist in its actual shipped targets (verified by reading `Nerdbank.GitVersioning.targets` directly on GitHub) — a real, verified doc/reality gap, not a configuration mistake. Adopting it faithfully would mean restructuring the release-cutting workflow itself (edit `version.json`, then `nbgv tag`), a bigger change than intended here.
- **MinVer:** considered first for its zero-config simplicity, and its documented "skip computation if `Version` is already set" behavior would likely have worked for the local-feed override (not hands-on tested, since GitVersion's tag-is-authoritative model turned out to fit even more directly). Rejected on reconsideration: single independent maintainer (`adamralph/minver`), no organizational backing, when this repo already tracks every non-trivial engineering decision via ADR and prefers tools with a defensible maintenance story.
- **Floating version in templates' `Directory.Packages.props`:** rejected — CPM does not support it (see above); would also make an end-user-facing pin implicitly track CI/dev state, which is the wrong direction.
- **Keep the script, just parameterize all four independently:** rejected — treats the symptom (one shared hardcoded default) without removing the "someone must remember to bump it" failure mode entirely.

## Related

- [ADR 0010: Extract Messaging and Shared Kernel as NuGet packages](./0010-extract-messaging-and-shared-kernel-as-nuget-packages.md)
- [ADR 0020: NuGet Trusted Publishing and Test-Gated Releases](./0020-nuget-trusted-publishing-and-test-gated-releases.md)
- [ADR 0025: Extract Dorn.WebUI.Primitives as NuGet package](./0025-extract-dorn-webui-primitives-as-nuget-package.md)
- [packages/Directory.Build.props](../../packages/Directory.Build.props)
