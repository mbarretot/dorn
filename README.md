# Dorn

[![CI](https://github.com/mbarretot/dorn/actions/workflows/ci.yml/badge.svg)](https://github.com/mbarretot/dorn/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)

.NET scaffolding CLI for Clean Architecture templates. Genera proyectos webapi listos para producción con arquitectura limpia, CQRS y persistencia con EF Core.

<p align="center">
  <img src="./docs/images/architecture.png" alt="Clean Architecture layers: Presentation, Infrastructure, Application, Domain" width="640">
</p>

## Quickstart

```bash
dotnet tool install -g dorn   # proximamente
dorn new webapi MyApp
cd MyApp && dotnet build
```

## Opciones

| Opción | Default | Descripción |
|---|---|---|
| `--database` | `sqlite` | `sqlite` (zero-config) o `sqlserver` (contenedor Aspire) |
| `--orchestrator` | `aspire` | `aspire` o `docker-compose` |
| `-o`, `--output` | directorio actual | Carpeta de salida |
| `--force` | — | Sobrescribir si la carpeta no está vacía |

```bash
dorn new webapi MyApp --database sqlserver --orchestrator docker-compose
```

## Workflow

<p align="center">
  <img src="./docs/images/workflow.png" alt="Workflow: install, create, extend, run and test" width="720">
</p>

## Alternativa: `dotnet new`

No necesitás instalar `dorn` — el template se instala como paquete NuGet standard:

```bash
pwsh eng/scripts/pack-templates.ps1
dotnet new install ./artifacts/Dorn.Templates.WebApi.*.nupkg
dotnet new dorn-webapi -n MyApp
```

## Estructura generada

```
MyApp/
├── src/
│   ├── MyApp.Domain/          # Entidades, eventos de dominio
│   ├── MyApp.Application/     # Commands, queries, handlers (CQRS)
│   ├── MyApp.Infrastructure/  # EF Core, persistencia
│   └── MyApp.WebApi/          # Minimal API endpoints
├── tests/
│   └── MyApp.Application.Tests/
├── MyApp.slnx
└── .editorconfig
```

Con `--orchestrator aspire` (default) se agrega `AppHost/` y `ServiceDefaults/`.

## Por qué Dorn

- **Sin licencias comerciales.** mediator CQRS MIT, sin MediatR ni FluentAssertions ni Moq.
- **Arquitectura limpia real.** Cuatro capas cableadas de punta a punta, no stubs.
- **Sin dependencia de herramientas externas.** Se embebe `Microsoft.TemplateEngine.Edge` — no toca el cache global de `dotnet new`.

## Documentación

- [Getting started](./docs/getting-started.md)
- [Referencia del template webapi](./docs/templates/webapi.md)
- [Decisiones de arquitectura](./docs/adr)
- [Contribuir](./docs/contributing.md)
