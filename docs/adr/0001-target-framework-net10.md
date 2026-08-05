# 0001. Target Framework: .NET 10

## Status

Accepted

## Context

Dorn is a community scaffolding CLI with a long lifespan; every project it generates
inherits its target framework choice. .NET alternates STS releases (18 months of
support) with LTS releases (3 years); LTS avoids forcing contributors to re-scaffold or
upgrade every 18 months. .NET 10 reached general availability as an LTS release in
November 2025, with support through November 2028.

## Decision

Dorn targets .NET 10 across `src/`, `tests/`, and the `webapi` template.

- The SDK version is pinned in `global.json` (`10.0.301` at the time of writing) with
  `rollForward: latestFeature`, tolerating later patch/feature releases without an edit
  per SDK update.

## Consequences

- Dorn and every generated project get three years of support (through November 2028)
  without a forced framework upgrade.
- Contributors and CI runners must have the .NET 10 SDK installed; older SDKs (6/8/9)
  are not sufficient.
- `Microsoft.TemplateEngine.*` packages (used by `Dorn.Core`, see ADR 0002) are pinned to
  the exact SDK version (`10.0.301`) in the root `Directory.Packages.props`, since they
  track the installed SDK tightly and likely need bumping in lockstep with it.
- Dorn does not currently target multiple TFMs (e.g. also `net8.0`); that would be a
  separate, larger decision affecting the CLI and every template.
