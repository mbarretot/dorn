# CleanArchGrpcService

[![Scaffolded with Dorn](https://img.shields.io/badge/scaffolded_with-Dorn-1A1A1A?style=flat-square)](https://github.com/mbarretot/dorn)

A Clean Architecture gRPC service with CQRS, EF Core, SQLite, and Aspire.

## ⚡ Start here

```bash
dotnet dev-certs https --trust
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
| `Grpc` | Protobuf contract, service adapter, and validation interceptor |
| `AppHost` and `ServiceDefaults` | Aspire orchestration, telemetry, and health checks |

The stack is intentionally fixed. There are no ORM, database, or orchestrator choices.

## 🧪 Test tiers

| Tier | Verifies |
| --- | --- |
| Application | Handlers, validators, behaviors, and domain logic |
| Integration | EF Core against a temporary SQLite database |
| Architecture | Layer dependency rules |
| Functional | gRPC round trip through `GrpcChannel` |

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

- [gRPC template reference](https://github.com/mbarretot/dorn/blob/main/docs/templates/grpc.md)
- [Dorn architecture decisions](https://github.com/mbarretot/dorn/tree/main/docs/adr)
