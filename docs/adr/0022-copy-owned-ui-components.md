# 0022. Copy-Owned UI Components, Not a NuGet Package

## Status

Accepted

## Context

The Blazor WebAssembly template ships seven design-system components
(`Components/Ui/`: Button, Card, Input+Label, Dialog, DropdownMenu, Tabs, Select) following the
shadcn/ui philosophy: components are generated source the consumer owns, not a versioned
dependency behind an opaque API. ADR 0010 already established the opposite choice for shared
CQRS/domain code — extract it into a NuGet package once a second template needs it — so this
ADR records why the UI layer takes the other path in v1.

## Decision

`Components/Ui/` generates as plain source inside `templates/blazor/wasm/`, not as a
`Dorn.WebUI.Primitives` package reference. The consumer can edit any component after
generation with no upgrade-path implications, matching shadcn/ui's own model.

Extraction to a shared `Dorn.WebUI.Primitives` package is deferred until
`templates/blazor/server` (or another Blazor hosting model) becomes a second real consumer —
mirroring ADR 0007's physical-copy-then-extract precedent, not skipping straight to a package
for a single consumer.

**Update (ADR 0024):** `templates/blazor/server` shipped and is now that second real consumer —
the trigger named above has fired. Extraction remains deliberately deferred; see ADR 0024's
Consequences and Alternatives for why.

**Update (ADR 0025):** extraction has now happened. The non-Razor primitive/interop/theme layer
moved to `packages/Dorn.WebUI.Primitives`; both templates consume it via `PackageReference`. The
decision recorded in *this* ADR — that `.razor` components stay copy-owned source, never packaged
— is unchanged and was explicitly reaffirmed as part of ADR 0025.

## Consequences

- Every generated app owns and can freely modify its design system with no package-version
  coupling.
- No breaking-change/migration story is needed for v1 — there is nothing to upgrade.
- A future second consumer (`templates/blazor/server`) triggers the same drift-then-extract
  decision ADR 0007 → ADR 0010 already walked for messaging/shared-kernel code.
- Bug fixes to the design system do not propagate automatically to already-generated projects;
  this is accepted as the direct cost of "you own the code."

## Alternatives

- **`Dorn.WebUI.Primitives` NuGet package from day one:** rejected — a package for a single
  consumer adds versioning and update-flow overhead with no present benefit, and forecloses
  nothing (ADR 0010's own precedent is to extract only once duplication is real).
- **A component library behind a compiled API surface (no source access):** rejected — directly
  contradicts the shadcn/ui philosophy the proposal chose, where owning and modifying the
  generated component source is the point.

## Related

- [ADR 0007: Superseded physical sync](./0007-templates-shared-physical-copy-sync.md)
- [ADR 0010: Extract messaging and shared kernel as NuGet packages](./0010-extract-messaging-and-shared-kernel-as-nuget-packages.md)
- [ADR 0023: Blazor WASM scoped MVP](./0023-blazor-wasm-scoped-mvp.md)
- [ADR 0024: Blazor Server template scoped MVP](./0024-blazor-server-scoped-mvp.md)
- [ADR 0025: Extract Dorn.WebUI.Primitives as a NuGet package](./0025-extract-dorn-webui-primitives-as-nuget-package.md)
