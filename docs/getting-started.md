# Getting Started

Install Dorn, generate a service, then build and test it. Contributors can use the same flow directly from the repository.

<p align="center">
  <img src="./images/dorn-flow.gif" alt="Animated Dorn workflow from CLI installation to a running service" width="820" />
</p>

## ⚡ Create your first service

```bash
dotnet tool install --global Dorn.Cli
dorn doctor
dorn new webapi MyApp
cd MyApp
dotnet build
dotnet dorn test
```

Run `dorn new webapi` without arguments in an interactive terminal for guided choices.

## 🧩 Choose a starting point

| Template | Command | Runtime profile |
| --- | --- | --- |
| Web API | `dorn new webapi MyApp` | Configurable database, ORM, orchestration, and auth |
| gRPC | `dorn new grpc MyService` | SQLite + EF Core + Aspire |
| Worker | `dorn new worker MyWorker` | SQLite + EF Core + Aspire + timer |

See the [Web API](./templates/webapi.md), [gRPC](./templates/grpc.md), and [worker](./templates/worker.md) guides.

## 🧑‍💻 Build Dorn locally

### Prerequisites

| Tool | Requirement |
| --- | --- |
| .NET SDK | `10.0.301` or a later .NET 10 feature band allowed by [`global.json`](../global.json) |
| PowerShell | `pwsh` for packaging scripts |
| Docker | Only for container-backed providers or Compose workflows |

```bash
git clone https://github.com/mbarretot/dorn.git
cd dorn
pwsh eng/scripts/pack-packages.ps1
dotnet restore Dorn.slnx
dotnet build Dorn.slnx -c Release
```

Local packages must be packed first because the raw templates restore unpublished changes from `./artifacts`.

## ▶️ Run the CLI from source

```bash
export DORN_TEMPLATES_PATH="$(pwd)/templates"
dotnet run --project src/Dorn.Cli -- new webapi MyApp
```

Generated output defaults to `./<name>`. Use `-o|--output` to choose a path, `--force` to overwrite a non-empty directory, or `--no-restore` to skip local tool restoration.

## 🧪 Run the repository checks

```bash
pwsh eng/scripts/pack-packages.ps1
dotnet build Dorn.slnx -c Release
DORN_TEMPLATES_PATH="$(pwd)/templates" \
  DORN_LOCAL_NUGET_FEED="$(pwd)/artifacts" \
  dotnet test Dorn.slnx
```

| Variable | Used for |
| --- | --- |
| `DORN_TEMPLATES_PATH` | Locating source templates from the CLI and generation tests |
| `DORN_LOCAL_NUGET_FEED` | Restoring locally packed Dorn packages from temporary generated projects |

`templates/tests` generates outside the repository and builds the result. That is the self-containment proof, not just a unit test.

## ⌨️ Work inside a generated project

| Command | Result |
| --- | --- |
| `dotnet dorn test` | Run all available test tiers |
| `dotnet dorn test --tier unit` | Run one tier |
| `dotnet dorn test --format json` | Run all tiers and emit a machine-readable JSON report |
| `dotnet dorn run` | Select Aspire, Compose, or plain .NET from project files |
| `dotnet dorn coverage` | Merge tier coverage and enforce the 80% gate |
| `dotnet dorn coverage --all` | Show every class in the coverage table |

### 🤖 `dorn test --format json`

Pass `--format json` to get one compact, single-line JSON document on stdout instead of the table renderer — useful for CI pipelines that need per-tier pass/fail counts without scraping console output. Table mode (the default) is unaffected.

```bash
dotnet dorn test --format json
```

```json
{"schemaVersion":1,"command":"test","success":true,"exitCode":0,"data":{"outcome":"ok","tierFilter":null,"tierFilterRecognized":null,"totalTests":16,"passedTests":16,"failedTests":0,"skippedTests":0,"durationSeconds":4.2,"reportUnavailableTiers":[],"tiers":[{"tier":"Application","outcome":"passed","countsAvailable":true,"total":9,"passed":9,"failed":0,"skipped":0,"durationSeconds":1.1}]}}
```

The envelope is the same `schemaVersion`/`command`/`success`/`exitCode`/`data` shape used by `dorn doctor` and `dorn coverage`. `data.outcome` is one of:

| Value | Meaning |
| --- | --- |
| `ok` | All attempted tiers passed |
| `tests-failed` | At least one tier failed (`exitCode: 1`) |
| `no-test-tiers` | The project was generated with `IncludeTests=false`; `tiers` is empty and `exitCode` stays `0` |

Each entry in `data.tiers[]` has its own `outcome` (`passed` \| `failed`), always derived from that tier's process exit code — independent of whether its report was readable. `countsAvailable` is `false`, and `total`/`passed`/`failed`/`skipped`/`durationSeconds` are `null`, when that tier's test report was missing or malformed; the tier's name is then also listed in the top-level `reportUnavailableTiers` array. A tier can be `passed` with `countsAvailable: false` — a reporting gap never flips the verdict or the exit code. Top-level `totalTests`/`passedTests`/`failedTests`/`skippedTests`/`durationSeconds` sum only tiers with `countsAvailable: true`, and are `null` when no tier reported counts.

`tierFilter` echoes the raw `--tier` value; `tierFilterRecognized` is `null` when `--tier` is omitted, `true` when it matched a known alias, and `false` when it did not (all tiers still run either way — an unrecognized `--tier` never narrows the run).

Out of scope: per-test-case detail (individual test names, failure messages, stack traces) is not part of this payload. Use the underlying `.trx` reports or a CI test-reporting plugin for that level of detail.

## 📦 Use vanilla `dotnet new`

Only the Web API template is distributed as a standalone template package:

```bash
dotnet new install Dorn.Templates.WebApi
dotnet new dorn-webapi -n MyApp
dotnet new uninstall Dorn.Templates.WebApi
```

The `dotnet new` cache and Dorn's isolated Template Engine cache do not interfere.

## 📚 Next steps

- Understand the [architecture](./architecture.md).
- Follow the [contributor workflow](./contributing.md).
- Review the [architecture decisions](./adr/).
