# Blazor WebAssembly Template

Generate a front-end-only Blazor WebAssembly app with a copy-owned, shadcn/ui-style design
system on Tailwind CSS v4, and Aspire orchestration.

## ⚡ Quick path

```bash
dorn new blazor wasm MyApp --theme rose
cd MyApp
dotnet build
dotnet dorn test
dotnet run --project src/MyApp.AppHost
```

## 🎯 Fixed profile

| Concern | Choice |
| --- | --- |
| Layers | Front-end only — no backend, no persistence, no mediator |
| Orchestrator | Aspire (always included, no opt-out) |
| Themes | `slate` (default), `rose` — each with light/dark |
| Components | Button, Card, Input+Label, Dialog, DropdownMenu, Tabs, Select |
| Tests | Application, Functional, Integration, Architecture — included by default |

The command accepts `<name>`, `-o|--output`, `--force`, `--no-restore`, `-t|--theme
<slate|rose>`, and `--no-playground`. Omitting `--theme` in an interactive terminal prompts for
a selection; a non-interactive session without the flag falls back to `slate`.

## 🏛️ Generated shape

| Project | Responsibility |
| --- | --- |
| `<Name>.Web` | Standalone Blazor WebAssembly app, design system, and playground |
| `<Name>.AppHost` | Aspire orchestration |

Inside `<Name>.Web`, `Components/Ui/` (the design system) never depends on `Features/` (app
code), and no app type touches `IJSRuntime` directly — both rules are enforced by the
Architecture test tier, not convention alone. `<Name>.Web` references the `Dorn.WebUI.Primitives`
NuGet package for class-merge, roving-focus/typeahead state, `UiId`/`UiValueComponent`/
`UiInputBase`, the JS-interop wrappers, and theme state; only the `.razor` components stay
copy-owned local source.

## 🎨 Theming

Two named themes ship as CSS custom properties (`--ui-*`), independent of the CLI's own
terminal branding (`DornPalette`/`IDornTheme`) — changing dorn's CLI colors never touches
generated app CSS, and vice versa. `--theme` sets the boot-time default; the runtime
`ThemeSwitcher` changes theme and light/dark mode without a page reload and persists the choice
to `localStorage`.

## 🧩 Playground

`IncludePlayground` defaults to `true` and generates `/playground` — one page per component
demonstrating live, interactive usage, plus an index. `--no-playground` produces a lean project
with no playground route or page files.

## 🚧 Same-name ambiguity

Component type names are unprefixed (`Button`, `Card`, `Input`, `Dialog`, `DropdownMenu`,
`Tabs`, `Select`), globally imported via `_Imports.razor`. Defining a `Features/` component with
the same name produces a Blazor ambiguous-reference compile error; disambiguate with a fully
qualified type name or an `@using` alias in the conflicting file.

## 🔌 Offline / air-gapped builds

The Tailwind CSS build step downloads a pinned, checksummed standalone CLI binary on first
build (see [ADR 0021](../adr/0021-tailwind-standalone-cli.md)). Set `DORN_TAILWIND_PATH` to a
local Tailwind CLI executable to skip the download entirely, or pre-warm
`$DORN_TOOLS_HOME/tailwindcss/<version>/<rid>/` (default `~/.dorn/tools`) from a machine with
network access. `dorn doctor` reports this as a Warn (never a Fail) when the binary cannot be
resolved by any of these paths.

## 🚫 Known gap

The three owned JS interop modules (`ui-modal.js`, `ui-dismiss.js`, `ui-anchor.js`) have no
automated browser-level test — bUnit renders into AngleSharp with no layout engine and no real
top layer, so it proves the C#/JS contract and ARIA state, not real focus movement or
positioning. Verified manually via the playground; no browser-automation test tier exists in
dorn today.

## 🧪 Test tiers

| Tier | Verifies |
| --- | --- |
| Application | Pure C# primitive logic: class-merge, roving tabindex, controlled/uncontrolled value |
| Functional | bUnit renders the real component tree, ARIA, and keyboard interaction |
| Integration | The Tailwind build pipeline produced real CSS with the expected tokens |
| Architecture | `Components/Ui/` never depends on `Features/`; no app type touches `IJSRuntime` directly |

## 📚 Related

- [ADR 0021: Tailwind standalone CLI acquisition](../adr/0021-tailwind-standalone-cli.md)
- [ADR 0022: Copy-owned UI components](../adr/0022-copy-owned-ui-components.md)
- [ADR 0023: Blazor WASM scoped MVP](../adr/0023-blazor-wasm-scoped-mvp.md)
- [Architecture](../architecture.md)
