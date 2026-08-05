# Getting Started

This guide covers building Dorn locally, running the CLI in development, and running the
test suite, for contributors. End users just install the published CLI
(`dotnet tool install --global Dorn.Cli`, then `dorn new webapi MyApp`); all five
`Dorn.*` packages are published at version `1.0.0`.

## Contents

- [Prerequisites](#prerequisites)
- [Environment variables](#environment-variables)
- [Build the repo locally](#build-the-repo-locally)
- [Run the CLI locally during development](#run-the-cli-locally-during-development)
- [Generated-project convenience verbs](#generated-project-convenience-verbs-dorn-test--dorn-run--dorn-coverage)
- [Alternative: install `templates/webapi` via vanilla `dotnet new`](#alternative-install-templateswebapi-via-vanilla-dotnet-new)
- [Run the tests](#run-the-tests)
- [Next steps](#next-steps)

## Prerequisites

- **.NET 10 SDK**, the version pinned in [`global.json`](../global.json) (currently
  `10.0.301`, `rollForward: latestFeature`: a later `10.0.x` feature-band SDK also works,
  nothing below `10.0.301`). Install from
  https://dotnet.microsoft.com/download/dotnet/10.0 if `dotnet --list-sdks` doesn't show
  a matching version.
- **pwsh (PowerShell)**, to run `eng/scripts/pack-packages.ps1` and
  `eng/scripts/pack-templates.ps1`. Install from
  https://learn.microsoft.com/powershell/scripting/install/installing-powershell if
  `pwsh --version` shows nothing.

## Environment variables

Two environment variables come up repeatedly, referenced from the command blocks below
rather than re-explained each time:

| Variable | Required for | Why |
| --- | --- | --- |
| `DORN_TEMPLATES_PATH` | Running the CLI from a checkout (`dotnet run --project src/Dorn.Cli`); `dotnet test Dorn.slnx` (`templates/tests`) | `TemplateLocator` needs the repo's `templates/` folder; the walk-up fallback is unreliable here (`dotnet run`'s output sits well below the repo root; `templates/tests` generates into `Path.GetTempPath()`, outside the repo). Without it, Dorn throws `DirectoryNotFoundException`. |
| `DORN_LOCAL_NUGET_FEED` | `dotnet test Dorn.slnx` (`templates/tests`) | `templates/tests` generates outside the repo, so it can't see the root `nuget.config`'s `dorn-local` source; the nested `dotnet restore` needs explicit `-p:RestoreAdditionalProjectSources` pointing at locally packed `Dorn.Messaging.Contracts`/`Dorn.Messaging`/`Dorn.SharedKernel`. Run `pwsh eng/scripts/pack-packages.ps1` first so `./artifacts` has content. |

## Build the repo locally

```bash
git clone https://github.com/mbarretot/dorn.git
cd dorn
pwsh eng/scripts/pack-packages.ps1
dotnet restore Dorn.slnx
dotnet build Dorn.slnx
```

`pack-packages.ps1` is a contributor step: `templates/webapi` consumes
`Dorn.Messaging.Contracts`, `Dorn.Messaging`, and `Dorn.SharedKernel` via
`PackageReference` (ADR 0010); it packs local copies into `./artifacts`, exposed by the
root `nuget.config` as the optional `dorn-local` source for unpublished package changes.
End users restore the published `1.0.0` packages from NuGet instead.

This builds all of `src/` (`Dorn.Abstractions`, `Dorn.Core`, `Dorn.Cli`), all of
`packages/` and `tests/`, and `templates/webapi`: a normal project reference inside
`Dorn.slnx`, so building the solution also confirms the template compiles standalone
(non-inherited `Directory.Build.props`/`Directory.Packages.props`; see
`docs/architecture.md`).

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
custom mediator; see `docs/templates/webapi.md`). `Dorn.Cli`'s embedded generation engine
needs the repo's `templates/` folder; `TemplateLocator` resolves this in order:

1. The `DORN_TEMPLATES_PATH` environment variable, if set (see [Environment
   variables](#environment-variables)): what you want when running from a checkout:

   ```bash
   export DORN_TEMPLATES_PATH="$(pwd)/templates"
   dotnet run --project src/Dorn.Cli -- new webapi MyApp
   ```

2. Otherwise, a walk up from the running assembly's base directory for a `templates/`
   folder containing at least one template. In source-checkout workflows, set
   `DORN_TEMPLATES_PATH` explicitly.

Generated output defaults to `./<name>` relative to your current directory; override with
`-o|--output`, and pass `--force` to overwrite a non-empty output directory.

## Generated-project convenience verbs (`dorn test` / `dorn run` / `dorn coverage`)

After generating a webapi project, three top-level verbs operate on it from the project
root (or any parent via `--project <path>`):

```bash
cd MyApp
dorn test              # all 4 tiers (Application/Integration/Architecture/Functional)
dorn test --tier unit  # one tier only
dorn run               # auto-detects AppHost → Aspire, Compose file → Compose, else plain `dotnet run`
dorn coverage          # tests + coverage collection + fixed 80% threshold gate
```

Two invocation surfaces, identical behavior:

- **`dorn <verb>`**: global tool, `dorn` on PATH.
- **`dotnet dorn <verb>`**: local tool via the generated project's
  `.config/dotnet-tools.json` (pinned `Dorn.Cli`, restored automatically by
  `dorn new webapi`; `--no-restore` to opt out, or run `dotnet tool restore` manually
  after a vanilla `dotnet new dorn-webapi`).

See `docs/templates/webapi.md` for full documentation of each verb.

## Alternative: install `templates/webapi` via vanilla `dotnet new`

Everything above runs the `dorn` CLI from source. `templates/webapi` is also packaged as
a standalone NuGet template package, installable with plain `dotnet new`, requiring no
`dorn` tool or checkout of this repo.

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

This uses the global `~/.templateengine` cache, separate from the `dorn` CLI's isolated
`~/.dorn/template-engine` host; the two don't interfere. See `docs/templates/webapi.md`
and `docs/adr/0008-dual-distribution-dotnet-new-template-pack.md` for details.

## Run the tests

```bash
DORN_TEMPLATES_PATH="$(pwd)/templates" DORN_LOCAL_NUGET_FEED="$(pwd)/artifacts" dotnet test Dorn.slnx
```

Both variables are needed for `templates/tests`; see [Environment
variables](#environment-variables) above.

- `templates/tests` generates a real `CleanArchWebApi` project into a temp directory
  outside the repo (`Path.GetTempPath()`) and runs `dotnet build` against it as a
  subprocess: proof the template is self-contained and buildable by an end user.
- `DORN_LOCAL_NUGET_FEED` is contributor/test-only; end users restore published packages
  from NuGet.

`tests/Dorn.Core.Tests` also runs the real Template Engine, against a minimal fixture
under `tests/Dorn.Core.Tests/Fixtures/minimal-fixture-template/` rather than the full
`webapi` template, so it doesn't need `DORN_TEMPLATES_PATH` (harmless either way).

## Next steps

- `docs/architecture.md`: how the three `src/` projects fit together (embedded Template
  Engine, custom mediator, shared packages).
- `docs/contributing.md`: conventions and the pre-PR verification loop.
- `docs/templates/webapi.md`: what the `webapi` template generates.
- `docs/adr/`: the full architecture decision records.
