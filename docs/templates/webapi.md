# Web API Template

Generate a Clean Architecture Minimal API with CQRS, four test tiers, and only the infrastructure choices you select.

## ⚡ Quick path

```bash
dorn new webapi MyApp
cd MyApp
dotnet build
dotnet dorn test
dotnet dorn run
```

In an interactive terminal, omit the name or option values for guided prompts. In scripts, defaults are deterministic.

## 🎛️ Generation choices

| Flag | Default | Values | Notes |
| --- | --- | --- | --- |
| `--orm` | `efcore` | `efcore`, `dapper` | Dapper removes EF Core context and migrations |
| `--database` | `sqlite` | `sqlite`, `sqlserver`, `postgres` | SQLite needs no server |
| `--orchestrator` | `aspire` | `aspire`, `docker-compose`, `none` | Controls local runtime scaffolding |
| `--auth` | `none` | `none`, `custom`, `azure-ad` | `custom` requires EF Core |
| `-o|--output` | `./<name>` | Path | Generated destination |
| `--force` | Off | Flag | Allows a non-empty output directory |
| `--no-restore` | Off | Flag | Skips `dotnet tool restore` |

Examples:

```bash
dorn new webapi MyApp --database postgres --orm dapper
dorn new webapi MyApp --orchestrator docker-compose
dorn new webapi MyApp --auth custom
dorn new webapi MyApp --auth azure-ad
```

> [!IMPORTANT]
> `--auth custom --orm dapper` is rejected. The custom user store and migrations require EF Core.

## 🏛️ Generated shape

| Project | Responsibility |
| --- | --- |
| `<Name>.Domain` | Entities, aggregate roots, and domain events |
| `<Name>.Application` | Commands, queries, handlers, validation, and ports |
| `<Name>.Infrastructure` | Selected EF Core or Dapper persistence |
| `<Name>.WebApi` | Minimal API endpoints and composition root |
| `<Name>.AppHost` | Aspire resources when selected |
| `<Name>.ServiceDefaults` | Aspire health, resilience, and service defaults when selected |

Dependencies point inward. The generated Architecture test project enforces the boundaries.

## ▶️ Run profile

| Orchestrator | Run command | Generated support |
| --- | --- | --- |
| `aspire` | `dotnet dorn run` | AppHost + ServiceDefaults |
| `docker-compose` | `dotnet dorn run` | Compose file + Dockerfile + local telemetry stack |
| `none` | `dotnet dorn run` | Direct Web API project |

`dotnet dorn run` detects the selected shape from project files. The equivalent direct commands are AppHost `dotnet run`, `docker compose up`, or Web API `dotnet run`.

## 💾 Persistence

| Choice | What is generated | Runtime requirement |
| --- | --- | --- |
| SQLite | Local file database | None |
| SQL Server | Provider-specific code and migrations | Docker for Aspire, Compose, and integration tests |
| PostgreSQL | Provider-specific code and migrations | Docker for Aspire, Compose, and integration tests |

EF Core generations include exactly one provider-specific migration set and apply it at startup. Dapper generations use provider-specific SQL and exclude EF Core files.

Unsupported providers such as MySQL or Oracle require a manual package, registration, connection string, migration, and orchestration swap.

## 🔁 CQRS and domain events

```text
Minimal API endpoint
  -> ISender
    -> pipeline behaviors
      -> request handler
        -> persistence
          -> domain events after a successful save
```

- Requests and notification contracts come from `Dorn.Messaging.Contracts`.
- `Dorn.Messaging` scans the Application assembly for handlers and behaviors.
- Aggregate roots own events through `Dorn.SharedKernel`.
- Notification handlers run sequentially and in-process.

See [ADR 0003](../adr/0003-custom-mediator-instead-of-mediatr.md), [ADR 0009](../adr/0009-ddd-aggregates-and-domain-events.md), and [ADR 0010](../adr/0010-extract-messaging-and-shared-kernel-as-nuget-packages.md).

## 🔐 Authentication

| Mode | Generated endpoints | Configuration |
| --- | --- | --- |
| `none` | None | None |
| `custom` | `/auth/login`, `/api/me` | Secret signing key; demo user seeds in Development |
| `azure-ad` | `/api/me` | Entra `Instance`, `TenantId`, and `ClientId` |

The custom signing key is never committed. Use user secrets or `Jwt__SigningKey`. Entra mode validates externally issued tokens and does not create a login endpoint or require a client secret.

## 📡 Observability

OpenTelemetry logging, metrics, and traces are always wired. Export happens only when a destination exists.

| Orchestrator | Destination |
| --- | --- |
| Aspire | Aspire dashboard |
| Docker Compose | Grafana, Loki, Prometheus, and Tempo |
| None | Your `OTEL_EXPORTER_OTLP_ENDPOINT` |

The Compose stack is for local evaluation: ephemeral storage and no production hardening.

## 🧪 Tests and project commands

| Tier | Verifies |
| --- | --- |
| Application | Handlers, validators, behaviors, and domain logic |
| Integration | Selected provider and real migrations or SQL |
| Architecture | Layer boundaries |
| Functional | HTTP routing, validation, and serialization |

```bash
dotnet dorn test
dotnet dorn test --tier integration
dotnet dorn coverage
dotnet dorn coverage --all
```

Coverage merges the freshest report from each tier, excludes generated and migration files, and enforces 80%. See [ADR 0019](../adr/0019-coverage-aggregation-merge-policy.md).

## 🔄 Tooling and CI

- `.config/dotnet-tools.json` pins `dorn.cli`; `dorn new webapi` restores it unless `--no-restore` is set.
- `.editorconfig` is the formatting source of truth. Use `dotnet format --verify-no-changes` to check it.
- `.github/workflows/ci.yml` runs the generated test workflow on Ubuntu and Windows. Container-backed provider setup is Linux-only.
- `global.json` pins the generated repository to the supported .NET 10 SDK feature band.

`IncludeTests` defaults to `true`. It is currently exposed only by vanilla `dotnet new`, not the Dorn command.

## 📦 Vanilla `dotnet new`

The Web API template is also a standalone NuGet template package:

```bash
dotnet new install Dorn.Templates.WebApi
dotnet new dorn-webapi -n MyApp
dotnet new uninstall Dorn.Templates.WebApi
```

This path uses the global `dotnet new` cache. Dorn's CLI uses its separate `~/.dorn/template-engine` cache.

## 📦 Template source

This template's source is migrating to [`mbarretot/dorn-templates-webapi`](https://github.com/mbarretot/dorn-templates-webapi),
which will become the source of truth and publish `Dorn.Templates.WebApi` from its own CI (see
[ADR 0028](../adr/0028-external-template-repos-webapi.md)). Today `templates/webapi/` in this repo is
still authoritative; `dorn new webapi` and the vanilla `dotnet new` path above are unaffected either way.

## 📚 Related decisions

[Persistence](../adr/0005-ef-core-sqlite-default-persistence.md) · [Database choices](../adr/0011-database-provider-selection.md) · [PostgreSQL](../adr/0014-postgresql-database-provider.md) · [Authentication](../adr/0016-opt-in-jwt-auth-scaffolding.md) · [Observability](../adr/0017-orchestrator-agnostic-observability.md) · [ADR 0028: External template repositories (webapi)](../adr/0028-external-template-repos-webapi.md)
