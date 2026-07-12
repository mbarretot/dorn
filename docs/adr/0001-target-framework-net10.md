# 0001. Target Framework: .NET 10

## Status

Accepted

## Context

Dorn is a new community scaffolding CLI intended to have a long lifespan, and every
project it generates inherits its target framework choice. .NET ships two kinds of
releases on alternating years: Standard Term Support (STS, 18 months) and Long Term
Support (LTS, 3 years). At the time this decision was made, .NET 10 had just reached
general availability (November 2025) as an LTS release, with support through November
2028. The development machine used to build this repository initially only had .NET
6/8/9 SDKs installed, none of which is .NET 10 — installing the .NET 10 SDK was a
blocking prerequisite before any code in this repo could compile.

A shorter-support STS release (e.g. a hypothetical .NET 11 in an off-year) would track
newer language/runtime features sooner, but would force Dorn and every project it
generates onto a much faster forced-upgrade cadence to stay in support — a poor fit for a
community tool whose users may not want to re-scaffold or manually upgrade their
generated project's target framework every 18 months.

## Decision

Dorn targets .NET 10 across `src/`, `tests/`, and the `webapi` template. The exact SDK
version is pinned in `global.json` (`10.0.301` at the time of writing) with
`rollForward: latestFeature`, so a checkout requires at least that SDK feature band but
tolerates later patch/feature releases within .NET 10 without requiring a `global.json`
edit for every SDK update.

## Consequences

- Dorn and every project it generates get three years of support (through November 2028)
  without a forced framework upgrade.
- Contributors and CI runners must have the .NET 10 SDK installed; older SDKs (6/8/9) are
  not sufficient, which was a real, encountered blocker during initial setup on this
  machine.
- `Microsoft.TemplateEngine.*` packages (used by `Dorn.Core`, see ADR 0002) are pinned to
  the exact SDK version (`10.0.301`) in the root `Directory.Packages.props`, since these
  packages track the installed SDK version tightly — bumping the SDK likely requires
  bumping these package versions in lockstep.
- Dorn does not currently target multiple TFMs (e.g. also `net8.0` for broader
  compatibility); doing so would be a separate, larger decision affecting both the CLI
  tool and every template.
