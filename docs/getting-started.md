# Getting Started

This guide covers building Dorn locally, running the CLI during development, and running
the test suite. It's aimed at contributors working inside a checkout of this repository.
End users can install the published CLI with `dotnet tool install --global Dorn.Cli` and
then run `dorn new webapi MyApp`; `Dorn.Cli`, `Dorn.Templates.WebApi`, `Dorn.Messaging`,
`Dorn.Messaging.Contracts`, and `Dorn.SharedKernel` are published as version `1.0.0` packages.

## Prerequisites

- **.NET 10 SDK**, exactly the version pinned in [`global.json`](../global.json)
  (currently `10.0.301`, with `rollForward: latestFeature`, meaning a later `10.0.x`
  feature-band SDK is also accepted but nothing below `10.0.301`). Install it from
  https://dotnet.microsoft.com/download/dotnet/10.0 if `dotnet --list-sdks` doesn't
  already show a matching version.
- **pwsh (PowerShell)** to run `eng/scripts/pack-packages.ps1` and
  `eng/scripts/pack-templates.ps1` — install from
  https://learn.microsoft.com/powershell/scripting/install/installing-powershell if
  `pwsh --version` doesn't already show one.

## Build the repo locally

```bash
git clone https://github.com/mbarretot/dorn.git
cd dorn
pwsh eng/scripts/pack-packages.ps1
dotnet restore Dorn.slnx
dotnet build Dorn.slnx
```

`pack-packages.ps1` is a contributor/local-development step: `templates/webapi` consumes
`Dorn.Messaging.Contracts`, `Dorn.Messaging`, and `Dorn.SharedKernel` via ordinary
`PackageReference` (see `docs/adr/0011-extract-messaging-and-shared-kernel-as-nuget-packages.md`).
The script packs local copies of those projects into `./artifacts`, and the root
`nuget.config` exposes that folder as the optional `dorn-local` source for testing
unpublished package changes. End users restore the published `1.0.0` packages from NuGet.

This builds all of `src/` (`Dorn.Abstractions`, `Dorn.Core`, `Dorn.Cli`), all of `packages/`
and `tests/`, and `templates/webapi` — the generated Clean Architecture Web API template is
a normal project reference inside `Dorn.slnx`, so building the solution also confirms the
template itself still compiles as a standalone project (see `docs/architecture.md` for why
`templates/webapi` has its own, non-inherited `Directory.Build.props` /
`Directory.Packages.props`).

For a Release build matching what CI runs:

```bash
dotnet build Dorn.slnx -c Release
```

## Run the CLI locally during development

The CLI is published as `Dorn.Cli`, but when changing it during development you run it
via `dotnet run` against the `Dorn.Cli` project:

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
   `templates/` folder that contains at least one template. In source-checkout workflows,
   set `DORN_TEMPLATES_PATH` explicitly so the development run uses this repository's
   templates.

If `DORN_TEMPLATES_PATH` isn't set and no fallback location is found, Dorn throws a
`DirectoryNotFoundException` with an explicit message telling you to set the variable.

Generated output defaults to `./<name>` relative to your current directory; override with
`-o|--output`, and pass `--force` to overwrite a non-empty output directory.

## Generated-project convenience verbs (`dorn test` / `dorn run` / `dorn coverage`)

After generating a webapi project, three new top-level verbs operate on it from the
project root (or any parent via `--project <path>`):

```bash
cd MyApp
dorn test              # all 4 tiers (Application/Integration/Architecture/Functional)
dorn test --tier unit  # one tier only
dorn run               # auto-detects AppHost → Aspire, Compose file → Compose, else plain `dotnet run`
dorn coverage          # tests + coverage collection + fixed 80% threshold gate
```

These work via two invocation surfaces:

- **`dorn <verb>`** — global tool, `dorn` on PATH (no `dotnet` prefix needed).
- **`dotnet dorn <verb>`** — local tool resolved via the generated project's
  `.config/dotnet-tools.json` (pinned `Dorn.Cli`, restored by `dorn new webapi`
  automatically; pass `--no-restore` to opt out, or run `dotnet tool restore` manually
  if you used vanilla `dotnet new dorn-webapi`).

Both surfaces produce identical behavior. See `docs/templates/webapi.md` for full
documentation of each verb.

## Alternative: install `templates/webapi` via vanilla `dotnet new`

Everything above runs the `dorn` CLI directly from source. `templates/webapi` is also
packaged as a standalone NuGet template package, installable with plain `dotnet new` and
requiring no `dorn` tool or checkout of this repo at all — this is also the mechanism
that makes the template discoverable in Visual Studio's "Create a new project" search.

```bash
dotnet new install Dorn.Templates.WebApi
dotnet new dorn-webapi -n MyApp
dotnet new uninstall Dorn.Templates.WebApi   # when you're done
```

Contributors testing unpublished template changes can optionally pack and install a local
`.nupkg` instead:

```bash
pwsh eng/scripts/pack-templates.ps1
dotnet new install ./artifacts/Dorn.Templates.WebApi.*.nupkg
```

This uses the global `~/.templateengine` cache (via `dotnet new install`/`uninstall`),
entirely separate from the isolated `~/.dorn/template-engine` host the `dorn` CLI uses —
the two channels don't interfere with each other. `Dorn.Templates.WebApi` is published as
version `1.0.0` on NuGet. See `docs/templates/webapi.md` and
`docs/adr/0009-dual-distribution-dotnet-new-template-pack.md` for details.

## Run the tests

```bash
DORN_TEMPLATES_PATH="$(pwd)/templates" DORN_LOCAL_NUGET_FEED="$(pwd)/artifacts" dotnet test Dorn.slnx
```

`DORN_TEMPLATES_PATH` is required for `tests/Templates.Tests`, which generates a real
`CleanArchWebApi` project into a temp directory outside the repo (`Path.GetTempPath()`)
and runs `dotnet build` against it as a subprocess — this is the test that proves the
template is genuinely self-contained and buildable by an end user, not just inside this
repo's solution. Without the environment variable set, that test (and anything else that
resolves the templates root without a fallback match) fails with the same
`DirectoryNotFoundException` described above.

`DORN_LOCAL_NUGET_FEED` is a contributor/test setting for the same test: the generated
project lives outside the repo, so it can't see the root `nuget.config`'s `dorn-local`
source, and the nested `dotnet restore` subprocess needs to be told explicitly where
locally packed `Dorn.Messaging.Contracts`/`Dorn.Messaging`/`Dorn.SharedKernel` packages
live (`-p:RestoreAdditionalProjectSources`). Run `pwsh eng/scripts/pack-packages.ps1`
first so `./artifacts` actually has something in it. End-user restores use the published
`1.0.0` packages from NuGet.

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
