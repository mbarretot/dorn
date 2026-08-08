<!-- prettier-ignore -->
<div align="center">

<img src="./docs/images/logo.png" alt="Dorn logo" align="center" height="64" />

# Dorn

**.NET scaffolding CLI with real Clean Architecture: no stubs, no placeholders.**

[![CI](https://github.com/mbarretot/dorn/actions/workflows/ci.yml/badge.svg)](https://github.com/mbarretot/dorn/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)
[![Dorn.Cli](https://img.shields.io/nuget/v/Dorn.Cli?style=flat-square&label=Dorn.Cli)](https://www.nuget.org/packages/Dorn.Cli)
[![Dorn.Templates.WebApi](https://img.shields.io/nuget/v/Dorn.Templates.WebApi?style=flat-square&label=Dorn.Templates.WebApi)](https://www.nuget.org/packages/Dorn.Templates.WebApi)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen?style=flat-square)](./docs/contributing.md)

[Quick start](#quick-start) • [Templates](#templates) • [Why Dorn](#why-dorn) • [Architecture](#architecture) • [CLI reference](#cli-reference) • [Documentation](#documentation)

</div>

---

> [!TIP]
> If this project helps you, leave a star.

Dorn is a .NET scaffolding CLI generating production-ready services with **Clean Architecture**, **CQRS**, and configurable persistence, wired end-to-end from commit one. Two templates ship today: a full-featured **`webapi`** (REST, choice of ORM/database) and a scoped, minimal **`grpc`** service.

## Quick start

```bash
dotnet tool install --global Dorn.Cli
dorn new webapi MyApp
cd MyApp && dotnet build
```

Prefer not to install a global tool? `webapi` also ships as a standard `dotnet new` template ([alternative installation](./docs/templates/webapi.md#alternative-vanilla-dotnet-new-without-the-dorn-cli)).

## Templates

|               | `webapi`                                               | `grpc`                                             |
| ------------- | ------------------------------------------------------ | -------------------------------------------------- |
| Generates     | REST API (ASP.NET Core Minimal APIs)                   | gRPC service (Protobuf)                            |
| Persistence   | EF Core or Dapper, your choice                         | EF Core (fixed)                                    |
| Database      | SQLite, SQL Server, or PostgreSQL                      | SQLite (fixed)                                     |
| Orchestration | Aspire, Docker Compose, or none                        | Aspire (fixed)                                     |
| Configuration | Flags or an interactive wizard                         | None (one fixed, opinionated MVP)                  |
| Reference     | [docs/templates/webapi.md](./docs/templates/webapi.md) | [docs/templates/grpc.md](./docs/templates/grpc.md) |

```bash
dorn new webapi MyApp --database postgres --orm dapper   # configurable
dorn new grpc MyService                                  # fixed scope, zero flags
```

`grpc` is a deliberately fixed MVP, not a smaller `webapi` ([scope rationale](./docs/templates/grpc.md#scope-a-fixed-mvp-not-a-smaller-webapi)).

## Why Dorn

<p align="center">
  <img src="./docs/images/architecture-illustrative.png" alt="A layered chevron mark: one command on the surface, a fully resolved architecture underneath" width="360">
</p>

`dorn new webapi MyApp` resolves, in one command, what you'd otherwise re-solve from scratch every time:

- **No commercial licenses anywhere**: a from-scratch, MIT-licensed CQRS mediator (no MediatR), xUnit + NSubstitute for tests (no FluentAssertions, no Moq)
- **The dependency rule is enforced, not just documented**: ArchUnitNET tests fail the build if a layer imports something it shouldn't
- **Four test tiers generated with the project**: Application, Integration, Architecture, Functional
- **Zero-config by default**: SQLite needs no external database; Aspire needs no Docker to get started
- **CI from the first push**: every generated project ships a working GitHub Actions workflow and a pinned `global.json`

## Architecture

```
MyApp/
├── src/
│   ├── MyApp.Domain/           # Entities, domain events, repository interfaces
│   ├── MyApp.Application/      # Commands, queries, handlers (CQRS), DTOs
│   ├── MyApp.Infrastructure/   # EF Core or Dapper implementations
│   └── MyApp.WebApi/           # Minimal API endpoints
└── tests/
    ├── MyApp.Application.Tests/     # Unit tests
    ├── MyApp.Architecture.Tests/    # Layering validation (ArchUnitNET)
    ├── MyApp.Functional.Tests/      # HTTP endpoints (WebApplicationFactory)
    └── MyApp.Integration.Tests/     # Real persistence (Testcontainers)
```

Dependencies point strictly inward:

- `WebApi` depends on `Application`
- `Infrastructure` implements interfaces that `Application` defines
- `Domain` depends on nothing

`Infrastructure` ships a Repository Pattern (`IRepository`, `IReadRepository`) with EF Core and Dapper implementations side by side, so `--orm` is a generation-time choice. Full breakdown: [docs/architecture.md](./docs/architecture.md).

## CLI reference

Every generated project ships verbs to operate on itself, from its root or any parent (`--project <path>`):

| Command         | Does                                                                                   |
| --------------- | -------------------------------------------------------------------------------------- |
| `dorn test`     | Runs all 4 tiers (`--tier` to filter to one)                                           |
| `dorn run`      | Auto-detects AppHost → Aspire, `docker-compose.yml` → Compose, else plain `dotnet run` |
| `dorn coverage` | Runs tests with coverage, gated at a fixed 80%                                         |

`dorn <verb>` and `dotnet dorn <verb>` (local tool, via `.config/dotnet-tools.json`) are equivalent.

Flags (`--orm`, `--database`, `--orchestrator`, `--auth`) are documented in the [template reference](./docs/templates/webapi.md).

## Roadmap

- [x] `webapi`: Clean Architecture, CQRS, EF Core/Dapper, 4 test tiers
- [x] `grpc`: Clean Architecture, CQRS, fixed EF Core/SQLite/Aspire MVP
- [ ] `ui`: placeholder at [`templates/ui/README.md`](./templates/ui/README.md)

Decisions behind these are recorded in [`docs/adr`](./docs/adr).

## Documentation

- [Getting started](./docs/getting-started.md): local development, from source
- [`webapi` template reference](./docs/templates/webapi.md)
- [`grpc` template reference](./docs/templates/grpc.md)
- [Architecture](./docs/architecture.md): how Dorn itself is built
- [Architecture decisions (ADRs)](./docs/adr)
- [Contributing](./docs/contributing.md)
