# Dorn.WebUI.Primitives

Framework-agnostic Blazor UI primitives shared by Dorn's Blazor templates: class merging (`Cn`/`ClassGroups`), roving-focus and typeahead state, `UiId`, `UiValueComponent`/`UiInputBase`, lazy JS-module interop wrappers, theme state, and toast queue state.

This package contains no `.razor` components. Components stay copy-owned per template — see [ADR 0022](https://github.com/mbarretot/dorn/blob/main/docs/adr/0022-copy-owned-ui-components.md).

## JS interop path contract

`ModalInterop`, `AnchorInterop`, and `DismissInterop` each wrap a JS module via `UiInteropModule`, which resolves its module with a dynamic `import()` relative to the consuming app's `document.baseURI`. This is a **runtime contract with no compile-time enforcement** — the compiler cannot verify a consumer ships the matching files.

A consuming app MUST keep its JS modules at exactly these paths, relative to `wwwroot`:

| Interop wrapper | Required module path |
| --- | --- |
| `ModalInterop` | `wwwroot/js/ui/ui-modal.js` |
| `AnchorInterop` | `wwwroot/js/ui/ui-anchor.js` |
| `DismissInterop` | `wwwroot/js/ui/ui-dismiss.js` |

If a module is missing or moved, the dynamic `import()` fails at runtime with a browser-level module resolution error, not a build-time error.

## Consumption

Reference the package and use the primitives directly:

```xml
<PackageReference Include="Dorn.WebUI.Primitives" />
```

```csharp
using Dorn.WebUI.Primitives;
using Dorn.WebUI.Primitives.Interop;
using Dorn.WebUI.Primitives.Theme;
using Dorn.WebUI.Primitives.Toast;
```
