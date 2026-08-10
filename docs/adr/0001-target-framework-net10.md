# 0001. Target Framework: .NET 10

## Status

Accepted

## Context

Generated projects inherit Dorn's framework choice. An LTS baseline avoids forcing users to upgrade after an 18-month STS window.

## Decision

Dorn and its generated templates target **.NET 10**.

- `global.json` pins SDK `10.0.301` with `rollForward: latestFeature`.
- `Microsoft.TemplateEngine.*` stays aligned with the SDK version.
- Dorn does not multi-target an older framework.

## Consequences

- The CLI and generated services share one supported platform.
- Contributors and CI need a compatible .NET 10 SDK.
- SDK and Template Engine upgrades must be coordinated.
- Adding another TFM would require a separate cross-repository decision.

## Related

- [ADR 0002: Embedded Template Engine](./0002-embed-template-engine-edge.md)
- [`global.json`](../../global.json)
