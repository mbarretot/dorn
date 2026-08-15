# 0025. Extract Dorn.WebUI.Primitives as a NuGet Package

## Status

Accepted. Supersedes in part ADR 0022; extends ADR 0007 -> ADR 0010.

## Context

ADR 0007 copied shared source into each template and used CI to detect drift; ADR 0010 replaced
that with packages once a second consumer of the messaging/shared-kernel code appeared. ADR 0022
deliberately took the copy-owned path for `Components/Ui/` instead, naming
`templates/blazor/server` becoming a second real consumer of the primitive/interop/theme layer as
the extraction trigger. ADR 0024 shipped that template — the trigger fired, and ADR 0022's own
Update note and ADR 0024's Decision table both recorded extraction as deliberately deferred at
that time, guarded only by a byte-parity test (`templates/tests/BlazorPrimitivesParityTests.cs`).

Duplication stopped being hypothetical. Both templates carried 15 byte-identical `.cs` files
(class merging, roving-focus/typeahead state, `UiId`, `UiValueComponent`/`UiInputBase`, four JS
interop wrappers, four theme types), and the parity test covered only the Primitives half of that
set — Theme was never guarded. Consolidating both templates' test suites (this change's own
earlier phase) surfaced two genuine, previously unrecorded drift gaps: `UiInputBaseInEditFormTests`
existed only in Server, and `{Modal,Dismiss,Anchor}InteropTests` existed only in WASM. Neither
template's suite alone covered the other's scenarios.

## Decision

Extract exactly the 15 non-Razor primitive/interop/theme types into `packages/Dorn.WebUI.Primitives`
under `Dorn.WebUI.Primitives[.Interop|.Theme]`. Plain `Microsoft.NET.Sdk`, targeting `net10.0` via
the root `Directory.Build.props`, referencing only `Microsoft.AspNetCore.Components`,
`Microsoft.AspNetCore.Components.Forms`, and `Microsoft.JSInterop`. The package versions
independently of `Dorn.SharedKernel`/`Dorn.Messaging`/`Dorn.Messaging.Contracts`, starting at
`1.0.0`, via a dedicated parameter in `eng/scripts/pack-packages.ps1`.

**Out of scope, reaffirming ADR 0022**: every `.razor` file. All seven UI components (Button,
Card, Dialog, DropdownMenu, Form, Select, Tabs), `ThemeSwitcher`, the Playground pages,
`wwwroot/js/**`, `Styles/themes/*.css`, and `build/Tailwind.targets` stay copy-owned source inside
each template. This was explicitly discussed and reaffirmed with the user before this change's SDD
cycle began — the shadcn/ui "you own the source" model ADR 0022 chose for components is unaffected;
only the framework-agnostic logic underneath the components moves.

The package publishes a normative JS-interop runtime contract (required `wwwroot/js/ui/*.js`
layout and exported function signatures) via a packed README, since that contract can no longer be
verified by both templates sharing the same source tree.

Both templates now reference the package via centrally-managed `PackageReference`, and the
now-vacuous `BlazorPrimitivesParityTests.cs` is deleted with no replacement — a shared version pin
is the new sync guarantee.

## Consequences

- One canonical implementation of the primitive/interop/theme layer; Theme is guarded for the
  first time.
- Cross-template sync becomes structural (an identical package version pin) instead of
  test-enforced. `BlazorPrimitivesParityTests` retires with no direct replacement.
- **Behavior change worth naming explicitly**: a bug fix to a primitive now propagates to both
  templates by bumping one `PackageVersion` entry per template, instead of via two separate,
  independently-reviewable template PRs each editing its own physical copy. Divergence becomes an
  explicit, reviewable version-pin change rather than silent copy drift — but the two templates are
  now coupled to a shared package version in a way they were not before ADR 0022's copy-owned
  model.
- Generated Blazor apps (WASM and Server) gain a fourth `Dorn.*` package dependency; local
  development must run `eng/scripts/pack-packages.ps1` before restoring, same as the existing
  messaging/shared-kernel packages.
- The JS-interop layout contract is now implicit and unverifiable at compile time in a way that was
  previously (accidentally) true only because both templates' `.cs` and `.js` files lived side by
  side — mitigated, not eliminated, by the packed README and the existing `<base href>` /
  interop-shape integration tests.
- Bug fixes still do not reach already-generated apps automatically; ADR 0022's original cost of
  "you own the code" survives one level down, at the package-version pin instead of the file copy.

## Alternatives

- **Keep duplicating (ADR 0022's status quo)**: rejected. Drift is no longer hypothetical — this
  change's own test-consolidation phase found two real, previously unrecorded gaps
  (`UiInputBaseInEditFormTests` Server-only, `{Modal,Dismiss,Anchor}InteropTests` WASM-only), and
  Theme was never guarded by any parity mechanism at all.
- **Extract the `.razor` components too**: rejected. Directly contradicts ADR 0022's shadcn/ui
  "you own the source" decision, which this ADR reaffirms rather than revisits. This was explicitly
  discussed and reaffirmed with the user before this SDD cycle started.
- **A second, separate `Dorn.WebUI.Theme` package**: rejected — four small types, identical
  consumers, identical release cadence as the rest of the primitives layer; splitting adds a
  version stream with no offsetting benefit.
- **Share the existing `1.0.1` version stream with the messaging/shared-kernel trio**: rejected —
  different consumer base (Blazor-hosting templates only, not webapi/grpc/worker), an ASP.NET Core
  dependency surface instead of pure-BCL, and UI-driven churn would force meaningless version bumps
  on unrelated templates.

## Related

- [ADR 0007: Superseded physical sync](./0007-templates-shared-physical-copy-sync.md)
- [ADR 0010: Extract messaging and shared kernel as NuGet packages](./0010-extract-messaging-and-shared-kernel-as-nuget-packages.md)
- [ADR 0022: Copy-owned UI components](./0022-copy-owned-ui-components.md)
- [ADR 0023: Blazor WASM scoped MVP](./0023-blazor-wasm-scoped-mvp.md)
- [ADR 0024: Blazor Server template scoped MVP](./0024-blazor-server-scoped-mvp.md)
- [Architecture](../architecture.md)
