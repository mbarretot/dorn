# Dorn templates

Source templates used by the Dorn CLI.

## 🧩 Available templates

| Command | Generates | Configuration | Reference |
| --- | --- | --- | --- |
| `dorn new webapi <name>` | ASP.NET Core Minimal API | ORM, database, orchestration, auth | [Guide](../docs/templates/webapi.md) |
| `dorn new grpc <name>` | gRPC service | Fixed EF Core, SQLite, Aspire | [Guide](../docs/templates/grpc.md) |
| `dorn new worker <name>` | Background worker | Fixed EF Core, SQLite, Aspire | [Guide](../docs/templates/worker.md) |

[`ui`](./ui/README.md) is reserved but not available.

## 📦 Shared building blocks

- [`Dorn.Messaging.Contracts`](../packages/Dorn.Messaging.Contracts/README.md): CQRS and notification contracts
- [`Dorn.Messaging`](../packages/Dorn.Messaging/README.md): mediator implementation
- [`Dorn.SharedKernel`](../packages/Dorn.SharedKernel/README.md): DDD primitives

Templates consume these as package references. See [ADR 0010](../docs/adr/0010-extract-messaging-and-shared-kernel-as-nuget-packages.md).

## 🚚 Distribution

All implemented templates work through `dorn new`. Web API also ships as a standard `dotnet new` template package.

To add a template, follow the [contributor guide](../docs/contributing.md#adding-a-new-template).
