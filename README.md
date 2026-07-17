<!-- prettier-ignore -->
<div align="center">

<img src="./docs/images/logo.png" alt="" align="center" height="64" />

# Dorn

[![CI](https://github.com/mbarretot/dorn/actions/workflows/ci.yml/badge.svg)](https://github.com/mbarretot/dorn/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/Dorn.Templates.WebApi?style=flat-square)](https://www.nuget.org/packages/Dorn.Templates.WebApi)

:star: If you like this project, star it on GitHub — it helps a lot!

[Overview](#overview) • [Get started](#getting-started) • [Options](#options) • [Architecture](#architecture) • [Documentation](#documentation) • [Contributing](#contributing)

</div>

.NET scaffolding CLI for Clean Architecture templates. Genera proyectos webapi listos para producción con arquitectura limpia, CQRS y persistencia configurable (EF Core o Dapper).

<p align="center">
  <img src="./docs/images/architecture.png" alt="Clean Architecture layers: Domain, Application, Infrastructure, WebApi" width="640">
</p>

## Overview

Dorn es una herramienta de scaffolding que genera proyectos .NET con Clean Architecture real — cuatro capas cableadas de punta a punta, no stubs ni placeholders.

Características principales:

- **Arquitectura limpia real** — Domain, Application, Infrastructure, WebApi completamente cableadas
- **CQRS nativo** — Commands y Queries separados con mediator pattern custom MIT (sin MediatR)
- **ORM flexible** — EF Core o Dapper, elegís según tu caso de uso
- **Testing completo** — Unit tests, Architecture tests (ArchUnitNET), Functional tests, Integration tests
- **CLI interactiva** — Opciones seleccionadas por wizard si no las pasás como flags

## Get started

### Instalación

```bash
dotnet tool install --global Dorn.Cli
```

El paquete publicado `Dorn.Cli` instala el ejecutable `dorn`.

### Uso básico

```bash
dorn new webapi MyApp
cd MyApp && dotnet build
```

O, opcionalmente, con el template publicado para `dotnet new`:

```bash
dotnet new install Dorn.Templates.WebApi
dotnet new dorn-webapi -n MyApp
```

### Verbos de conveniencia en el proyecto generado

Una vez generado, el proyecto incluye tres verbos que operan sobre él desde la raíz
(o cualquier padre con `--project <path>`):

```bash
dorn test              # corre los 4 tiers (Application / Integration / Architecture / Functional)
dorn test --tier unit  # un solo tier
dorn run               # auto-detecta AppHost → Aspire, docker-compose.yml → Compose, sino `dotnet run` plain
dorn coverage          # tests + cobertura + gate fijo al 80%
```

Las dos formas de invocación son equivalentes:

- **`dorn <verbo>`** — global tool (PATH).
- **`dotnet dorn <verbo>`** — local tool resuelta por `.config/dotnet-tools.json` que
  `dorn new webapi` ya genera (pinned a `Dorn.Cli`, restaurado automáticamente).

Ver [docs/templates/webapi.md](./docs/templates/webapi.md) para detalles.

### Desarrollo local (desde source)

Los flujos con paquetes `.nupkg` locales y feeds bajo `./artifacts` son solo para contributors y desarrollo local; para uso publicado, instala `Dorn.Cli` desde NuGet. Ver [Getting started](./docs/getting-started.md).

## Options

| Option | Default | Description |
|---|---|---|
| `--orm` | `efcore` | ORM: `efcore` (EF Core with migrations) or `dapper` (micro-ORM with raw SQL) |
| `--database` | `sqlite` | Database provider: `sqlite` (zero-config) or `sqlserver` (Aspire container) |
| `--orchestrator` | `aspire` | Orchestrator: `aspire` or `docker-compose` |
| `-o`, `--output` | current directory | Output folder |
| `--force` | — | Overwrite if folder is not empty |

### Examples

```bash
# Full stack: Dapper + SQL Server + Docker Compose
dorn new webapi MyApp --orm dapper --database sqlserver --orchestrator docker-compose

# Default: EF Core + SQLite + Aspire
dorn new webapi MyApp

# Minimal: EF Core + SQLite + no orchestrator
dorn new webapi MyApp --orchestrator none
```

## Architecture

### Clean Architecture Layers

<p align="center">
  <img src="./docs/images/workflow.png" alt="Workflow: install, create, extend, run and test" width="720">
</p>

```
MyApp/
├── src/
│   ├── MyApp.Domain/           # Entities, domain events, repository interfaces
│   ├── MyApp.Application/     # Commands, queries, handlers (CQRS), DTOs
│   ├── MyApp.Infrastructure/  # EF Core or Dapper implementations
│   └── MyApp.WebApi/         # Minimal API endpoints
└── tests/
    ├── MyApp.Application.Tests/      # Unit tests
    ├── MyApp.Architecture.Tests/    # Layer validation (ArchUnitNET)
    ├── MyApp.Functional.Tests/      # HTTP endpoints (WebApplicationFactory)
    └── MyApp.Integration.Tests/     # Real persistence (Testcontainers)
```

### ORM Selection

| ORM | When to use | Features |
|---|---|---|
| **EF Core** | Default, auto migrations, change tracking | `DbContext`, migrations, `SaveChanges` automático |
| **Dapper** | Maximum control, optimized queries, raw SQL | Connection factory, explicit queries, maximum performance |

### Repository Pattern

El template implementa Repository Pattern en el dominio:

```
Domain/Common/Interfaces/
├── IRepository.cs          # Generic: GetByIdAsync, Add, Update, Remove
├── IReadRepository.cs      # Read-only: GetAllAsync, FindAsync, AnyAsync
└── ITodoItemRepository.cs  # Entity-specific (extensible)

Infrastructure/Repositories/
├── EfCore/TodoItemRepository.cs    # EF implementation
└── Dapper/TodoItemRepository.cs   # Dapper implementation
```

## Technology Stack

- **.NET 10** con C# 13 (latest)
- **Microsoft.TemplateEngine.Edge** embebido (no toca cache global de `dotnet new`)
- **Paquetes NuGet publicados** — `Dorn.Cli`, `Dorn.Templates.WebApi`, `Dorn.Messaging`, `Dorn.Messaging.Contracts` y `Dorn.SharedKernel` en versión `1.0.0`
- **Mediator pattern** custom MIT (sin MediatR)
- **EF Core 10** o **Dapper 2.1** según opción seleccionada
- **xUnit + NSubstitute + ArchUnitNET** para tests
- **Spectre.Console** para CLI interactiva

## Features

- **Sin licencias comerciales** — mediator CQRS MIT, sin FluentAssertions ni Moq
- **Migrations automáticas** — solo con EF Core (con `dotnet ef migrations add`)
- **Docker support** — Docker Compose o Aspire para desarrollo local
- **Zero-config SQLite** — funciona out-of-the-box sin base de datos externa
- **Type-safe validation** — FluentValidation para commands y queries

## Documentation

- [Getting started](./docs/getting-started.md)
- [WebAPI template reference](./docs/templates/webapi.md)
- [Architecture decisions](./docs/adr)
- [Contributing](./docs/contributing.md)

## Contributing

Este proyecto acepta contribuciones. Ver [CONTRIBUTING](./docs/contributing.md) para guidelines.

## License

Este proyecto está bajo licencia MIT. Ver [LICENSE](./LICENSE) para más detalles.
