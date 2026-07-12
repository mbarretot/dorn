# Templates

Every scaffolding template Dorn can generate, plus the shared source library they build on.

## Sync convention

`shared/` is the **canonical source** for cross-template building blocks (base domain types, the custom mediator, etc.). Each template — `webapi/` today, `ui/` in the future — keeps a **physical copy** of these files inside its own tree, rather than referencing `shared/` directly.

That's a deliberate trade-off, not an oversight: templates need to be fully self-contained so they can be packaged and consumed as standalone NuGet template packages, and an MSBuild `<Compile Include>` reaching outside a template's own root doesn't survive packaging. See ADR 0008 for the full reasoning.

The cost of that trade-off is drift — nothing stops the two copies from diverging over time. `eng/scripts/check-shared-sync.sh` closes that gap: it diffs `shared/` against every template's copy and fails CI on any mismatch. If you change a file under `shared/`, propagate the change to every template that carries a copy of it in the same pull request.

## Layout

- `shared/` — canonical source, not a compiled project.
- `webapi/` — Clean Architecture Minimal API template.
- `ui/` — placeholder for a future Blazor template.
