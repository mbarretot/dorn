# Engineering

Build, packaging, and release tools for this repository. Nothing here ships inside generated projects.

## ⚡ Common tasks

| Goal | Command |
| --- | --- |
| Pack shared packages | `dotnet pack packages/<Name>/<Name>.csproj -c Release -o ./artifacts` (per package — no wrapper script; version comes from GitVersion, see ADR 0026) |
| Vendor the webapi template | `pwsh eng/scripts/vendor-webapi-templates.ps1` (see [ADR 0028](../docs/adr/0028-external-template-repos-webapi.md)) |
| Vendor the blazor templates | `pwsh eng/scripts/vendor-blazor-templates.ps1` (see [ADR 0027](../docs/adr/0027-external-template-repos-blazor-first.md)) |
| Smoke-test a packed CLI | `pwsh eng/scripts/smoke-test-cli.ps1` |

Packages are written to `./artifacts`. `Dorn.Templates.WebApi` and the blazor template packs are packed and published from their own repos' CI, not from here (ADR 0028, ADR 0027).

## 📦 Release flow

| Step | Behavior |
| --- | --- |
| Trigger | Push a `vX.Y.Z` tag |
| Gate | Reusable Linux and Windows test matrix must pass |
| Version | Computed by GitVersion from the tag ([ADR 0026](../docs/adr/0026-gitversion-for-package-versioning.md)) |
| Authentication | NuGet Trusted Publishing through OIDC |
| Output | Four libraries and `Dorn.Cli` |

> [!IMPORTANT]
> Do not rename `.github/workflows/publish.yml`. The NuGet Trusted Publishing policy is bound to that workflow name.

For the rationale and manual prerelease check, read [ADR 0020](../docs/adr/0020-nuget-trusted-publishing-and-test-gated-releases.md).
