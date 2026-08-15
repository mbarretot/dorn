# 0024. Blazor Server Template: Scoped MVP

## Status

Accepted

## Context

`templates/blazor/server/` is dorn's first Blazor Server app and the second member of the
blazor family. ADR 0023 set the family's scoped-MVP precedent for WASM; ADR 0015 and ADR 0018
set the general one for gRPC and worker. ADR 0022 named `templates/blazor/server` becoming a
second real consumer of `Components/Ui/Primitives/` as the trigger for extracting a shared
`Dorn.WebUI.Primitives` package. This ADR records that the trigger has now fired, and that
extraction is nonetheless deliberately deferred.

## Decision

v1 ships with these fixed choices:

| Concern | v1 scope |
| --- | --- |
| Layers | Front-end only — identical to 0023. A Blazor Server app could host a backend; v1 deliberately does not, so the two templates differ only in hosting model |
| Hosting | `Microsoft.NET.Sdk.Web`; global `InteractiveServer` render mode on `<Routes>` and `<HeadOutlet>` |
| Prerendering | Enabled (framework default). Interop safety is guaranteed by the `OnAfterRenderAsync` lifecycle gate, promoted from a doc-comment convention to an Architecture-tier fitness function |
| Circuit configuration | Explicit non-goal — no `CircuitOptions` tuning, no custom reconnect UI, no `PersistentComponentState` handoff. Framework defaults only, asserted by a regression test |
| Orchestrator | Aspire, always included, three projects (`Web`, `ServiceDefaults`, `AppHost`) — diverging from 0023's two-project WASM shape |
| Primitives sharing | Explicit non-goal — no `packages/Dorn.WebUI.Primitives`; an independent physical copy guarded by a byte-parity test |
| Themes / Components / Playground | Identical to 0023 (`slate`/`rose`, seven components, `IncludePlayground` default `true`) |

## Consequences

- The blazor family is now internally inconsistent on orchestrator project count (WASM 2,
  Server 3) — deliberate, driven by a hosting fact, not taste: OpenTelemetry exporters, health
  endpoints, and service discovery are dead code in a browser sandbox and live code in a server
  process.
- `theme-boot.js` remains the sole writer of `data-ui-theme`/`data-ui-mode`; the server never
  emits them, because it cannot read `localStorage` and any guess reintroduces the flash 0023's
  decision removed.
- Two near-identical primitives copies now exist; a byte-parity test makes drift a build failure
  and is the extraction-readiness signal ADR 0022 asked for.
- Prerendering means `UiId` mints different ids in the prerender and interactive passes; Blazor
  patches them on connect, ARIA linkage stays internally consistent at every observable instant,
  and removing the divergence would require the out-of-scope `PersistentComponentState` handoff.
- Circuit tuning is the first thing real Server apps hit in production; omitting it is a
  deliberate honesty choice, not an oversight.
- `dorn run`/`dorn doctor` need zero CLI change because both detection probes are path-shape
  based, not template-name-aware.

## Alternatives

- **Disable prerendering:** rejected — removes the interop risk class but trades first paint for
  a loading flash and drops the teaching value of the hardest part of Server hosting.
- **Mirror WASM's two-project AppHost shape:** rejected — see Consequences; a real server process
  needs ServiceDefaults' observability and health-check wiring, a browser sandbox does not.
- **Extract `Dorn.WebUI.Primitives` in this change:** rejected — ADR 0007/0010's precedent is to
  let duplication become observable first; bundling extraction here would make both the port and
  the extraction unreviewable in one PR.
- **Per-component `@rendermode` opt-in:** rejected for v1 — a uniform global render mode is
  simplest to reason about; the render-mode story is a later teaching change.
- **webapi's `AddObservability()` split:** rejected — it exists only to survive webapi's
  orchestrator axis (Aspire/Compose/none), which this template does not have.

## Related

- [ADR 0012: Four-tier test strategy](./0012-four-tier-test-strategy.md)
- [ADR 0015: gRPC template scoped MVP](./0015-grpc-template-scoped-mvp.md)
- [ADR 0017: Orchestrator-agnostic observability](./0017-orchestrator-agnostic-observability.md)
- [ADR 0018: Worker template scoped MVP](./0018-worker-template-scoped-mvp.md)
- [ADR 0021: Tailwind standalone CLI](./0021-tailwind-standalone-cli.md)
- [ADR 0022: Copy-owned UI components](./0022-copy-owned-ui-components.md)
- [ADR 0023: Blazor WASM scoped MVP](./0023-blazor-wasm-scoped-mvp.md)
