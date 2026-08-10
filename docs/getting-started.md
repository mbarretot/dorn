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
| `dotnet dorn run` | Select Aspire, Compose, or plain .NET from project files |
| `dotnet dorn coverage` | Merge tier coverage and enforce the 80% gate |
| `dotnet dorn coverage --all` | Show every class in the coverage table |

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
