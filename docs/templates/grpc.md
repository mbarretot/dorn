# gRPC Template

Generate a focused Clean Architecture gRPC service with SQLite, EF Core, Aspire, and the same CQRS core as the Web API template.

## ⚡ Quick path

```bash
dorn new grpc MyService
cd MyService
dotnet dev-certs https --trust
dotnet dorn test
dotnet dorn run
```

## 🎯 Fixed profile

| Concern | Choice |
| --- | --- |
| Database | SQLite |
| ORM | EF Core |
| Orchestrator | Aspire |
| Transport | gRPC over TLS and HTTP/2 |
| Tests | Included by default |

The command accepts `<name>`, `-o|--output`, `--force`, and `--no-restore`. It intentionally has no database, ORM, or orchestrator flags.

## 🏛️ Generated shape

| Project | Responsibility |
| --- | --- |
| `<Name>.Domain` | Entities, aggregates, and domain events |
| `<Name>.Application` | CQRS requests, handlers, validation, and ports |
| `<Name>.Infrastructure` | EF Core, SQLite migrations, and repositories |
| `<Name>.Grpc` | Proto contract, service adapter, and validation interceptor |
| `<Name>.AppHost` | Aspire orchestration |
| `<Name>.ServiceDefaults` | Telemetry, health, and resilience defaults |

## 🔌 RPC surface

| RPC | Dispatches |
| --- | --- |
| `CreateTodoItem` | `CreateTodoItemCommand` |
| `GetTodoItems` | `GetTodoItemsQuery` |

The wire package remains `todo.v1`. Only the C# namespace follows the generated project name, preventing invalid proto identifiers.

```text
gRPC request
  -> TodoGrpcService
    -> ISender
      -> validation behavior
        -> handler
```

`ValidationInterceptor` converts `FluentValidation.ValidationException` into gRPC `InvalidArgument` status.

## 🌐 Runtime detail

Aspire is the supported run path:

```bash
dotnet run --project src/MyService.AppHost
```

Kestrel uses `Http1AndHttp2` over TLS. HTTP/2 serves gRPC, while HTTP/1.1 keeps Aspire health probes working on the same endpoint.

> [!NOTE]
> Do not add HTTPS redirection to the gRPC host. gRPC clients cannot follow the resulting redirect response.

## 🧪 Test tiers

| Tier | Verifies |
| --- | --- |
| Application | Handlers, validators, behaviors, and domain logic |
| Integration | Real migrations against a temporary SQLite file |
| Architecture | Layer dependency rules |
| Functional | RPC round trip through `GrpcChannel` |

Functional tests adapt TestServer responses to HTTP/2 so `Grpc.Net.Client` accepts the in-memory transport.

## 🚧 Intentional limits

- No database, ORM, or orchestrator choices.
- No generated CI workflow.
- No standalone `Dorn.Templates.Grpc` NuGet template package.
- No update, delete, or streaming RPCs.

Generate through `dorn new grpc`; vanilla `dotnet new install` is not available for this template.

## 📚 Related

- [Scoped MVP decision](../adr/0015-grpc-template-scoped-mvp.md)
- [Architecture](../architecture.md)
- [Web API CQRS details](./webapi.md#-cqrs-and-domain-events)
