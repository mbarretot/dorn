# eng/

Engineering scripts and tooling that support the Dorn repository itself — as opposed to
`src/`, `templates/`, and `tests/`, which are the product. Nothing under `eng/` ships to
end users of `dorn` or of a generated project.

## Layout

```
eng/
├── README.md
├── packaging/
│   └── Dorn.Templates.WebApi/   # pack-only project, not part of Dorn.sln
│       └── Dorn.Templates.WebApi.csproj
└── scripts/
    ├── check-shared-sync.sh   # enforced in CI, see below
    └── pack-templates.ps1     # packages templates/webapi as a dotnet-new NuGet template
```

## `scripts/check-shared-sync.sh`

`templates/shared/` holds the canonical source for code that is also needed, verbatim,
inside `templates/webapi/`:

- `templates/shared/Domain/*.cs` → `templates/webapi/src/CleanArchWebApi.Domain/*.cs`
- `templates/shared/Application/Messaging/*.cs` → `templates/webapi/src/CleanArchWebApi.Application/Messaging/*.cs`

This is a **physical copy**, not a symlink and not an MSBuild `<Compile Include>` that
reaches outside the template's own directory tree. The reason is packaging: a future
`dotnet new install`-style NuGet template package (see `pack-templates.ps1` below) can
only include files that live inside the template's own root — `templates/webapi/` must
stay fully self-contained. A symlink would also break on checkouts/CI runners that don't
preserve them consistently across Windows and Linux (the CI matrix in
`.github/workflows/ci.yml` runs both).

The tradeoff of a physical copy is drift: nothing stops someone from editing
`templates/webapi/src/CleanArchWebApi.Domain/BaseEntity.cs` directly and forgetting to
port the change back to `templates/shared/Domain/BaseEntity.cs` (or vice versa).
`check-shared-sync.sh` closes that gap by diffing every file in the canonical `shared/`
locations against its corresponding copy and failing (non-zero exit, with a `diff -u` of
every mismatch) if they've diverged. It runs as its own job in CI
(`.github/workflows/ci.yml`, job `check-shared-sync`) on every push and pull request.

Run it locally after touching `templates/shared/` or the corresponding files in
`templates/webapi/`:

```bash
eng/scripts/check-shared-sync.sh
```

See `docs/adr/0008-templates-shared-physical-copy-sync.md` for the full decision record.

## `scripts/pack-templates.ps1`

Packages `templates/webapi` as a standalone NuGet template package
(`<PackageType>Template</PackageType>`), installable via vanilla
`dotnet new install Dorn.Templates.WebApi` — independent of the `dorn` CLI tool, and
discoverable in Visual Studio's "Create a new project" search via the same
`PackageType=Template` mechanism. This is the "dual distribution" path: `templates/webapi`
stays installable through the `dorn` CLI's own embedded Template Engine host (ADR 0002)
*and* through this NuGet package — both point at the same `templates/webapi/` content,
so there's no drift risk between the two channels.

What it does:

1. Runs `eng/scripts/check-shared-sync.sh` first and fails fast on drift (a stale
   physical copy must not ship inside the package).
2. Runs `dotnet pack` against `eng/packaging/Dorn.Templates.WebApi/Dorn.Templates.WebApi.csproj`
   — a pack-only project **outside** `templates/webapi/`, so the packaging project itself
   never gets instantiated into a generated user project. It is not part of `Dorn.sln`;
   invoke it directly via `dotnet pack <path>` or through this script.
3. Emits the `.nupkg` to `./artifacts`, versioned via an optional `-Version` parameter
   (default `0.1.0-dev`).

```bash
pwsh eng/scripts/pack-templates.ps1
pwsh eng/scripts/pack-templates.ps1 -Version 1.2.3
```

See `docs/adr/0009-dual-distribution-dotnet-new-template-pack.md` for the full decision
record, including why this was originally deferred and later brought into v1 scope.

## TODO

The following are deferred, tracked here rather than silently dropped:

- **Coverage upload pipeline.** `ci.yml` already collects coverage via
  `dotnet test --collect:"XPlat Code Coverage"`, but the results aren't uploaded
  anywhere (Codecov, workflow artifact, PR comment, etc.) yet. Marked with a
  `# TODO(eng):` comment in `.github/workflows/ci.yml`.
- **Release / publish pipeline.** No tag-triggered job exists yet to `dotnet pack` and
  `dotnet nuget push` the `dorn` CLI as a published global tool, nor to publish
  `Dorn.Templates.WebApi` (built by `pack-templates.ps1`) to NuGet.org. Marked with a
  `# TODO(eng):` comment in `.github/workflows/ci.yml`. Until this exists,
  `dotnet new install Dorn.Templates.WebApi` only works against a local
  `./artifacts/*.nupkg` path, not by package ID.
