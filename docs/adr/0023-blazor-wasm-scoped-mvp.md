# 0023. Blazor WASM Template: Scoped MVP

## Status

Accepted

## Context

`templates/blazor/wasm/` is dorn's first front-end template. ADR 0015 and ADR 0018 already set
the precedent of shipping a fixed, deliberately narrow MVP profile for a new template family
(gRPC, worker) rather than replicating `webapi`'s full configurability on day one. The proposal
round (`sdd/feat-blazor-ui-template/proposal-resolutions`) settled the v1 boundary; this ADR
records it.

## Decision

v1 ships with these fixed choices:

| Concern | v1 scope |
| --- | --- |
| Layers | Front-end only — no Domain/Application/Infrastructure, no backend, no persistence, no mediator |
| Orchestrator | Aspire only, always included — no opt-out flag, no Compose/plain alternative |
| Themes | Two named themes (`slate`, `rose`), each with light/dark — independent of the CLI's own `DornPalette`/`IDornTheme` terminal branding |
| Components | Seven: Button, Card, Input+Label, Dialog, DropdownMenu, Tabs, Select |
| Playground | `IncludePlayground` defaults to `true`; `--no-playground` produces a lean project |

## Consequences

- `dorn new blazor wasm <name>` has a small, predictable flag surface (`--theme`,
  `--no-playground`) instead of a webapi-sized configuration matrix.
- A full-stack story (auth, API scaffolding, a real backend) is explicitly out of scope for v1
  and is a distinct future proposal, not an incremental flag.
- Fixing Aspire removes an entire branch of doctor/CLI logic; `ProjectContextResolver` already
  detects Aspire from the `*.AppHost` folder, so `dorn run` needs zero new code.
- Theme identity staying independent of `DornPalette` means the CLI's own terminal branding can
  evolve without ever touching generated app CSS, and vice versa.
- Expanding orchestrator choice, backend scope, or the component set are each separate, future
  decisions — not silently implied by this ADR.

## Alternatives

- **Configurable orchestrator (Aspire/Compose/none), mirroring webapi:** rejected for v1 — the
  front-end-only scope makes Compose/plain meaningfully redundant with Aspire's dev-server
  role; deferred alongside the full-stack decision.
- **Ship the full shadcn/ui component catalog:** rejected — seven components each proving a
  distinct primitive class (variant merge, slot composition, controlled/uncontrolled state,
  focus trap, roving tabindex, anchored positioning, listbox pattern) is enough to validate the
  design system's architecture without an unbounded initial surface.
- **`IncludePlayground` defaulting to `false`:** rejected — the playground is the template's
  shop window; opting out should be the deliberate action, not the default.

## Related

- [ADR 0015: gRPC template scoped MVP](./0015-grpc-template-scoped-mvp.md)
- [ADR 0018: Worker template scoped MVP](./0018-worker-template-scoped-mvp.md)
- [ADR 0022: Copy-owned UI components](./0022-copy-owned-ui-components.md)
