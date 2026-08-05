# Templates

Templates available for generating projects with Dorn.

## Available templates

| Name     | Description                                                            | Reference                                                |
| -------- | ------------------------------------------------------------------------ | ----------------------------------------------------------- |
| `webapi` | Clean Architecture REST API, CQRS, choice of ORM/database/orchestrator | [docs/templates/webapi.md](../docs/templates/webapi.md) |
| `grpc`   | Clean Architecture gRPC service, fixed SQLite + EF Core + Aspire MVP    | [docs/templates/grpc.md](../docs/templates/grpc.md)     |
| `ui`     | Blazor template, not yet implemented                                   | [templates/ui/README.md](./ui/README.md)                |

## Shared building blocks

Code common to every template (`Entity`, `AggregateRoot`, `Result`, the CQRS mediator) ships as NuGet packages under `packages/`, not copied per template:

- `Dorn.SharedKernel`: base domain types
- `Dorn.Messaging.Contracts`: mediator interfaces
- `Dorn.Messaging`: mediator implementation

Consumed via `PackageReference`. See [ADR 0010](../docs/adr/0010-extract-messaging-and-shared-kernel-as-nuget-packages.md).

## Distribution

Every template generates from the same content here, through up to two channels:

1. **`dorn new <name>`**: via the Dorn CLI (all templates)
2. **`dotnet new <name>`**: via a standalone NuGet template package (`webapi` only today; see [ADR 0008](../docs/adr/0008-dual-distribution-dotnet-new-template-pack.md) and [`grpc`'s current gap](../docs/templates/grpc.md#alternative-vanilla-dotnet-new-without-the-dorn-cli))

## Adding a new template

See [Contributing](../docs/contributing.md#adding-a-new-template).
