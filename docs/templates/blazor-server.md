# Blazor Server Template

Generate a front-end-only Blazor Server app with a copy-owned, shadcn/ui-style design system on
Tailwind CSS v4, Interactive Server rendering, and Aspire orchestration.

## ⚡ Quick path

```bash
dorn new blazor server MyApp --theme rose
cd MyApp
dotnet build
dotnet dorn test
dotnet run --project src/MyApp.AppHost
```

## 🎯 Fixed profile

| Concern | Choice |
| --- | --- |
| Layers | Front-end only — no backend, no persistence, no mediator |
| Hosting | `Microsoft.NET.Sdk.Web`, `AddInteractiveServerComponents`, prerendering enabled |
| Orchestrator | Aspire (always included, no opt-out), three projects (`Web`, `ServiceDefaults`, `AppHost`) |
| Circuit config | Framework defaults only — no `CircuitOptions` tuning, no custom reconnect UI |
| Themes | `slate` (default), `rose` — each with light/dark |
| Components | 24 components across Forms, Overlays, Display, Layout, and Feedback — see `/playground` for the full, searchable catalog |
| Tests | Application, Functional, Integration, Architecture — included by default |

The command accepts `<name>`, `-o|--output`, `--force`, `--no-restore`, `-t|--theme
<slate|rose>`, and `--no-playground` — the identical flag surface as `dorn new blazor wasm`.
Omitting `--theme` in an interactive terminal prompts for a selection; a non-interactive session
without the flag falls back to `slate`.

## 🏛️ Generated shape

| Project | Responsibility |
| --- | --- |
| `<Name>.Web` | Blazor Server app, design system, and playground |
| `<Name>.ServiceDefaults` | OpenTelemetry, health checks, service discovery — dead code in WASM's browser sandbox, live code in a real server process |
| `<Name>.AppHost` | Aspire orchestration |

Inside `<Name>.Web`, `Components/Ui/` (the design system) never depends on `Features/` (app
code), and no app type touches `IJSRuntime` directly — both rules are enforced by the
Architecture test tier, not convention alone. `<Name>.Web` references the `Dorn.WebUI.Primitives`
NuGet package for class-merge, roving-focus/typeahead state, `UiId`/`UiValueComponent`/
`UiInputBase`, the JS-interop wrappers, and theme state; only the `.razor` components (including
`ThemeSwitcher`) stay copy-owned local source.

## 🎨 Theming

Same two-theme system as `blazor wasm`, unaware of the CLI's own terminal branding
(`DornPalette`/`IDornTheme`). `--theme` sets the boot-time default via a classic, synchronous
`theme-boot.js` script that runs before first paint, whether the document was streamed by Kestrel
or served from disk. The server never emits `data-ui-theme`/`data-ui-mode` itself — it cannot
read `localStorage`, and a guessed value would reintroduce the flash the boot script exists to
prevent. The runtime `ThemeSwitcher` changes theme and light/dark mode without a page reload and
persists the choice to `localStorage`.

## ⚙️ Prerendering and interop safety

Prerendering is enabled — the ASP.NET Core Interactive Server default. Interop-bearing
components (Dialog, DropdownMenu, Select) never touch JavaScript before the SignalR circuit
connects because every JS call is gated behind `OnAfterRenderAsync`, which the framework never
invokes during the static prerender pass. This is enforced by an Architecture-tier fitness
function, not just convention. If you add a new interop-bearing component, gate its JS calls the
same way — `OnParametersSet` sets a pending flag, `OnAfterRenderAsync` acts on it.

For non-lifecycle interop needs, `RendererInfo.IsInteractive` is the documented escape hatch: it
distinguishes prerender from a connected circuit directly, at the cost of a Server-only API
inside the `Dorn.WebUI.Primitives` package.

## 🧩 Playground

`IncludePlayground` defaults to `true` and generates `/playground` — a layout with a searchable
left-rail nav and one `ComponentPlayground` page per component, all rendered under Interactive
Server rendering. `--no-playground` produces a lean project with no playground route or page
files.

Each page renders the same shell: a live **Preview** next to interactive **Controls** bound to
real component parameters via plain Blazor two-way binding (no `DynamicComponent` or runtime
reflection), a generated **Code** snippet with a copy-to-clipboard button, and an **API** table
listing every documented parameter, its type, default, and description. The nav groups all 24
components into five categories — Forms, Overlays, Display, Layout, Feedback — behind a live
search box that filters by label or keyword and auto-expands matching categories.

## 🔌 Circuit behavior

No `CircuitOptions` are configured beyond framework defaults, and no custom reconnection UI
ships. A dropped SignalR circuit that reconnects within the framework's default retention window
resumes the same circuit and component instances against an untouched DOM; interop handles
(scroll lock, dismiss listeners, anchored positioning) stay valid because no render — and
therefore no interop call — happens while disconnected. Eviction past the retention period forces
a full page reload. Tuning either of these is explicitly out of scope for v1.

## 🚧 Same-name ambiguity

Identical to `blazor wasm`: component type names are unprefixed and globally imported via
`_Imports.razor`. Defining a `Features/` component with the same name produces a Blazor
ambiguous-reference compile error; disambiguate with a fully qualified type name or an `@using`
alias in the conflicting file.

## 🔌 Offline / air-gapped builds

Same Tailwind CLI acquisition mechanism as `blazor wasm` (see
[ADR 0021](../adr/0021-tailwind-standalone-cli.md)). Set `DORN_TAILWIND_PATH` to a local Tailwind
CLI executable to skip the download, or pre-warm `$DORN_TOOLS_HOME/tailwindcss/<version>/<rid>/`
from a machine with network access. `dorn doctor` reports this as a Warn, never a Fail.

## 🚫 Known gap

Same as `blazor wasm`: the four owned JS interop modules (`ui-modal.js`, `ui-dismiss.js`,
`ui-anchor.js`, `ui-clipboard.js`) have no automated browser-level test — bUnit proves the C#/JS
contract and ARIA state, not real focus movement, positioning, or clipboard access. Verified
manually via the playground and, for Dialog specifically, via a one-time manual go/no-go check
covering prerender-with-JS-disabled and circuit disconnect/reconnect.

## 🧪 Test tiers

| Tier | Verifies |
| --- | --- |
| Application | Pure C# primitive logic: class-merge, roving tabindex, controlled/uncontrolled value, `UiInputBase` inside a real `EditForm`+`EditContext` |
| Functional | bUnit renders the real component tree, ARIA, keyboard interaction, and that no interop call happens before the first `OnAfterRenderAsync` |
| Integration | The Tailwind build pipeline produced real, fingerprinted CSS; the root document (`GET /`) carries the correct theme-boot script, no server-emitted theme attributes, and working health endpoints |
| Architecture | `Components/Ui/` never depends on `Features/`; no app type touches `IJSRuntime` directly; interop calls only originate from `OnAfterRenderAsync`/`DisposeAsync`; no `CircuitOptions`/reconnect-UI type exists |

Note the one tier-placement divergence from `blazor wasm`: the root-document assertions live in
Integration (`WebApplicationFactory<Program>`), not Functional — they verify what the build
pipeline actually produced, the same tier that already owns the Tailwind CSS assertions.

## 📚 Related

- [ADR 0012: Four-tier test strategy](../adr/0012-four-tier-test-strategy.md)
- [ADR 0017: Orchestrator-agnostic observability](../adr/0017-orchestrator-agnostic-observability.md)
- [ADR 0021: Tailwind standalone CLI acquisition](../adr/0021-tailwind-standalone-cli.md)
- [ADR 0022: Copy-owned UI components](../adr/0022-copy-owned-ui-components.md)
- [ADR 0023: Blazor WASM scoped MVP](../adr/0023-blazor-wasm-scoped-mvp.md)
- [ADR 0024: Blazor Server template scoped MVP](../adr/0024-blazor-server-scoped-mvp.md)
- [ADR 0025: Extract Dorn.WebUI.Primitives as a NuGet package](../adr/0025-extract-dorn-webui-primitives-as-nuget-package.md)
- [Architecture](../architecture.md)
- [Blazor WASM template](./blazor-wasm.md)
