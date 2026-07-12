# Getting Started

This guide covers building Dorn locally, running the CLI during development, and running
the test suite. It's aimed at contributors working inside a checkout of this repository —
not at end users of a published `dorn` tool (that workflow is `dotnet tool install -g dorn`
followed by `dorn new webapi MyApp`, and isn't available yet; see the root `README.md`).

## Prerequisites

- **.NET 10 SDK**, exactly the version pinned in [`global.json`](../global.json)
  (currently `10.0.301`, with `rollForward: latestFeature`, meaning a later `10.0.x`
  feature-band SDK is also accepted but nothing below `10.0.301`). Install it from
  https://dotnet.microsoft.com/download/dotnet/10.0 if `dotnet --list-sdks` doesn't
  already show a matching version.
- A `bash`-capable shell if you intend to run `eng/scripts/check-shared-sync.sh` (macOS
  and Linux have this natively; on Windows use WSL or Git Bash).

## Build the repo locally

```bash
git clone https://github.com/mbarretot/dorn.git
cd dorn
dotnet restore Dorn.sln
dotnet build Dorn.sln
```

This builds all of `src/` (`Dorn.Abstractions`, `Dorn.Core`, `Dorn.Cli`), all of `tests/`,
and `templates/webapi` — the generated Clean Architecture Web API template is a normal
project reference inside `Dorn.sln`, so building the solution also confirms the template
itself still compiles as a standalone project (see `docs/architecture.md` for why
`templates/webapi` has its own, non-inherited `Directory.Build.props` /
`Directory.Packages.props`).

For a Release build matching what CI runs:

```bash
dotnet build Dorn.sln -c Release
```

## Run the CLI locally during development

The CLI isn't published anywhere yet, so during development you run it via `dotnet run`
against the `Dorn.Cli` project:

```bash
dotnet run --project src/Dorn.Cli -- new webapi MyApp
```

This generates a new `MyApp/` directory (Clean Architecture layers, EF Core + SQLite,
custom mediator — see `docs/templates/webapi.md`). `Dorn.Cli`'s embedded generation engine
needs to find the repo's `templates/` folder; `TemplateLocator` resolves this in order:

1. The `DORN_TEMPLATES_PATH` environment variable, if set — this is what you want when
   running from a checkout, since `dotnet run`'s working/output directory is several
   levels below the repo root:

   ```bash
   export DORN_TEMPLATES_PATH="$(pwd)/templates"
   dotnet run --project src/Dorn.Cli -- new webapi MyApp
   ```

2. Otherwise, it walks up from the running assembly's base directory looking for a
   `templates/` folder that contains at least one template — this covers a future
   packaged-tool layout that ships `templates/` next to the tool, but there's no such
   packaging yet, so in practice you should always set `DORN_TEMPLATES_PATH` when running
   from source.

If `DORN_TEMPLATES_PATH` isn't set and no fallback location is found, Dorn throws a
`DirectoryNotFoundException` with an explicit message telling you to set the variable.

Generated output defaults to `./<name>` relative to your current directory; override with
`-o|--output`, and pass `--force` to overwrite a non-empty output directory.

## Alternative: install `templates/webapi` via vanilla `dotnet new`

Everything above runs the `dorn` CLI directly from source. `templates/webapi` is also
packaged as a standalone NuGet template package, installable with plain `dotnet new` and
requiring no `dorn` tool or checkout of this repo at all — this is also the mechanism
that makes the template discoverable in Visual Studio's "Create a new project" search.

```bash
pwsh eng/scripts/pack-templates.ps1
dotnet new install ./artifacts/Dorn.Templates.WebApi.*.nupkg
dotnet new dorn-webapi -n MyApp
dotnet new uninstall Dorn.Templates.WebApi   # when you're done
```

This uses the global `~/.templateengine` cache (via `dotnet new install`/`uninstall`),
entirely separate from the isolated `~/.dorn/template-engine` host the `dorn` CLI uses —
the two channels don't interfere with each other. `Dorn.Templates.WebApi` isn't published
to NuGet.org yet, so `dotnet new install` above points at a locally built `.nupkg`. See
`docs/templates/webapi.md` and `docs/adr/0009-dual-distribution-dotnet-new-template-pack.md`
for details.

## Run the tests

```bash
DORN_TEMPLATES_PATH="$(pwd)/templates" dotnet test Dorn.sln
```

`DORN_TEMPLATES_PATH` is required for `tests/Templates.Tests`, which generates a real
`CleanArchWebApi` project into a temp directory outside the repo (`Path.GetTempPath()`)
and runs `dotnet build` against it as a subprocess — this is the test that proves the
template is genuinely self-contained and buildable by an end user, not just inside this
repo's solution. Without the environment variable set, that test (and anything else that
resolves the templates root without a fallback match) fails with the same
`DirectoryNotFoundException` described above.

`tests/Dorn.Core.Tests` also runs the real Template Engine, but against a minimal fixture
under `tests/Dorn.Core.Tests/Fixtures/minimal-fixture-template/` rather than the full
`webapi` template, so it doesn't need `DORN_TEMPLATES_PATH` — setting it anyway is
harmless.

## Next steps

- `docs/architecture.md` — how the three `src/` projects fit together and the key
  implementation decisions (embedded Template Engine, custom mediator, `shared/` sync).
- `docs/contributing.md` — conventions and the verification loop to run before a PR.
- `docs/templates/webapi.md` — what the `webapi` template generates.
- `docs/adr/` — the full architecture decision records.
