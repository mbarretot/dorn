# Dorn

[![CI](https://github.com/mbarretot/dorn/actions/workflows/ci.yml/badge.svg)](https://github.com/mbarretot/dorn/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

.NET scaffolding CLI for Clean Architecture templates. Genera proyectos webapi listos para producción con arquitectura limpia, CQRS y persistencia configurable.

<p align="center">
  <img src="./docs/images/architecture.png" alt="Clean Architecture layers: Presentation, Infrastructure, Application, Domain" width="640">
</p>

## Quickstart

```bash
dotnet tool install -g dorn
dorn new webapi MyApp
cd MyApp && dotnet build
```

## Opciones

| Opción | Default | Descripción |
|---|---|---|
| `--orm` | `efcore` | `efcore` (Entity Framework Core con migrations) o `dapper` (micro-ORM con SQL raw) |
| `--database` | `sqlite` | `sqlite` (zero-config) o `sqlserver` (contenedor Aspire) |
| `--orchestrator` | `aspire` | `aspire` o `docker-compose` |
| `-o`, `--output` | directorio actual | Carpeta de salida |
| `--force` | — | Sobrescribir si la carpeta no está vacía |

```bash
# Con Dapper y SQL Server
dorn new webapi MyApp --orm dapper --database sqlserver --orchestrator docker-compose

# Default: EF Core + SQLite + Aspire
dorn new webapi MyApp
```

## Architecture

<p align="center">
  <img src="./docs/images/workflow.png" alt="Workflow: install, create, extend, run and test" width="720">
</p>

### ORM Selection

| ORM | Cuándo usarlo | Características |
|---|---|---|
| **EF Core** | Default, migrations automáticas, change tracking | `DbContext`, migrations, `SaveChanges` automático |
| **Dapper** | Control máximo, queries optimizadas, SQL raw | Connection factory, queries explícitas, máximo performance |

### Repository Pattern

El template implementa Repository Pattern en el dominio:

```
Domain/Common/Interfaces/
├── IRepository.cs          # Genérico: GetByIdAsync, Add, Update, Remove
├── IReadRepository.cs      # Solo lectura: GetAllAsync, FindAsync, AnyAsync
└── ITodoItemRepository.cs  # Específico por entidad (extensible)

Infrastructure/Repositories/
├── EfCore/TodoItemRepository.cs    # Implementación EF
└── Dapper/TodoItemRepository.cs   # Implementación Dapper
```

## Alternativa: `dotnet new`

No necesitás instalar `dorn` — el template se instala como paquete NuGet standard:

```bash
pwsh eng/scripts/pack-templates.ps1
dotnet new install ./artifacts/Dorn.Templates.WebApi.*.nupkg
dotnet new dorn-webapi -n MyApp
dotnet new uninstall Dorn.Templates.WebApi
```

## Estructura generada

```
MyApp/
├── src/
│   ├── MyApp.Domain/          # Entidades, eventos de dominio, interfaces de repository
│   ├── MyApp.Application/     # Commands, queries, handlers (CQRS), DTOs
│   ├── MyApp.Infrastructure/  # Implementaciones: EF Core o Dapper repositories
│   └── MyApp.WebApi/          # Minimal API endpoints
├── tests/
│   ├── MyApp.Application.Tests/
│   ├── MyApp.Architecture.Tests/    # Validación de capas (ArchUnitNET)
│   ├── MyApp.Functional.Tests/     # HTTP endpoints (WebApplicationFactory)
│   └── MyApp.Integration.Tests/   # Persistencia real (Testcontainers)
├── MyApp.slnx
└── .editorconfig
```

Con `--orchestrator aspire` (default) se agrega `AppHost/` y `ServiceDefaults/`.

## Por qué Dorn

- **Sin licencias comerciales.** mediator CQRS MIT, sin MediatR ni FluentAssertions ni Moq.
- **Arquitectura limpia real.** Cuatro capas cableadas de punta a punta, no stubs.
- **ORM flexible.** EF Core o Dapper — elegís según tu caso de uso.
- **Testing completo.** Tests unitarios, de arquitectura, funcionales e integración.

## Documentación

- [Getting started](./docs/getting-started.md)
- [Referencia del template webapi](./docs/templates/webapi.md)
- [Decisiones de arquitectura](./docs/adr)
- [Contribuir](./docs/contributing.md)

## Technology Stack

- **.NET 10** con C# 13 (latest)
- **Microsoft.TemplateEngine.Edge** embebido (no toca cache global)
- **Mediator pattern** custom MIT (sin MediatR)
- **EF Core 10** o **Dapper 2.1** según opción
- **xUnit + NSubstitute + ArchUnitNET** para tests
- **Spectre.Console** para CLI interactiva
