# Engineering

Build, packaging, and release tools for this repository. Nothing here ships inside generated projects.

## ⚡ Common tasks

| Goal | Command |
| --- | --- |
| Pack shared packages | `pwsh eng/scripts/pack-packages.ps1` |
| Pack the Web API template | `pwsh eng/scripts/pack-templates.ps1` |
| Smoke-test a packed CLI | `pwsh eng/scripts/smoke-test-cli.ps1` |

Packages are written to `./artifacts`. The template packaging project lives in `eng/packaging/` so generated projects never receive it.

## 📦 Release flow

| Step | Behavior |
| --- | --- |
| Trigger | Push a `vX.Y.Z` tag |
| Gate | Reusable Linux and Windows test matrix must pass |
| Version | Derived from the tag |
| Authentication | NuGet Trusted Publishing through OIDC |
| Output | Three libraries, `Dorn.Cli`, and `Dorn.Templates.WebApi` |

> [!IMPORTANT]
> Do not rename `.github/workflows/publish.yml`. The NuGet Trusted Publishing policy is bound to that workflow name.

For the rationale and manual prerelease check, read [ADR 0020](../docs/adr/0020-nuget-trusted-publishing-and-test-gated-releases.md).
