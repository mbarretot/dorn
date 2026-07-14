# Templates

Templates disponibles para generar proyectos con Dorn.

## Plantillas

| Nombre | Descripción |
|---|---|
| `webapi` | Clean Architecture Minimal API con CQRS y EF Core |
| `ui` | Blazor template (proximamente) |

## Building blocks compartidos

El codigo comun a todos los templates (`Entity`, `AggregateRoot`, `Result`, mediator CQRS) vive en paquetes NuGet distribuidos en `packages/`:

- `Dorn.SharedKernel` — tipos de dominio base
- `Dorn.Messaging.Contracts` — interfaces del mediator
- `Dorn.Messaging` — implementacion del mediator

Se consumen via `PackageReference`, no se copian por template. Ver [ADR 0011](./docs/adr/0011-extract-messaging-and-shared-kernel-as-nuget-packages.md).

## Distribucion

Cada template se distribuye de dos formas:

1. **`dorn new <nombre>`** — via el CLI de Dorn
2. **`dotnet new <nombre>`** — via paquete NuGet standalone

Ambos canales generan desde el mismo contenido en este directorio.
