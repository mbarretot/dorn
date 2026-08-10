# CleanArchWorkerService

[![Scaffolded with Dorn](https://img.shields.io/badge/scaffolded_with-Dorn-1A1A1A?style=flat-square)](https://github.com/mbarretot/dorn)

A Clean Architecture background worker with CQRS, EF Core, SQLite, and Aspire.

## ⚡ Start here

```bash
dotnet tool restore
dotnet dorn run
```

Verify the project:

```bash
dotnet dorn test
```

## 🏛️ Project map

| Area | Responsibility |
| --- | --- |
| `Domain` | Entities, aggregates, and domain events |
| `Application` | Commands, queries, handlers, validation, and ports |
| `Infrastructure` | EF Core and SQLite persistence |
| `Worker` | `BackgroundService` processing loop |
| `AppHost` and `ServiceDefaults` | Aspire orchestration, telemetry, and health checks |

The stack is intentionally fixed. There are no ORM, database, or orchestrator choices.

## ⏱️ Runtime

- `Worker:Interval` in `appsettings.json` controls the loop interval. Default: `00:00:30`.
- Each tick creates a dependency injection scope for scoped database services.
- `/health` and `/alive` let Aspire report worker health.

## 🧪 Test tiers

| Tier | Verifies |
| --- | --- |
| Application | Handlers, validators, behaviors, and domain logic |
| Integration | EF Core against a temporary SQLite database |
| Architecture | Layer dependency rules |
| Functional | Hosted worker loop driven by `TimeProvider` |

No test tier requires Docker.

## ⌨️ Project CLI

| Command | Action |
| --- | --- |
| `dotnet dorn run` | Run the Aspire AppHost |
| `dotnet dorn test` | Run every tier |
| `dotnet dorn test --tier <name>` | Run one tier |
| `dotnet dorn coverage` | Test with the 80% coverage gate |

> [!NOTE]
> This scoped template does not generate a CI workflow yet.

## 📚 Details

- [Worker template reference](https://github.com/mbarretot/dorn/blob/main/docs/templates/worker.md)
- [Dorn architecture decisions](https://github.com/mbarretot/dorn/tree/main/docs/adr)
