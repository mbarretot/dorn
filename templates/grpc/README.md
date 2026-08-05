# CleanArchGrpcService

[![Scaffolded with Dorn](https://img.shields.io/badge/scaffolded_with-Dorn-1A1A1A?style=flat-square)](https://github.com/mbarretot/dorn)

A Clean Architecture gRPC service: Domain, Application, Infrastructure, and a proto-backed presentation layer fully wired, CQRS via a custom mediator, on a fixed SQLite + EF Core + Aspire stack.

## 🚀 Getting started

```bash
dotnet dev-certs https --trust   # one-time per machine, gRPC requires TLS/HTTP2
dotnet run --project src/CleanArchGrpcService.AppHost
```

Aspire isn't optional here: there's no `--orchestrator` flag, so the AppHost is the only supported way to run this service.

> [!TIP]
> `dorn test`, `dorn run`, and `dorn coverage` also work from this project's root; see [CLI commands](#cli-commands) below.

## 📁 Project structure

```
src/
├── CleanArchGrpcService.Domain/            # Entities, domain events, no dependencies
├── CleanArchGrpcService.Application/       # Commands, queries, handlers, validators, behaviors
├── CleanArchGrpcService.Infrastructure/    # EF Core DbContext, SQLite migration
├── CleanArchGrpcService.Grpc/              # Protos/todo.proto, TodoGrpcService, Program.cs
├── CleanArchGrpcService.AppHost/           # Aspire AppHost: registers the service as a resource
└── CleanArchGrpcService.ServiceDefaults/   # Aspire ServiceDefaults: telemetry, health checks, resilience
tests/
├── CleanArchGrpcService.Application.Tests/    # Unit: handlers, validators, behaviors
├── CleanArchGrpcService.Integration.Tests/    # Real EF Core persistence against a temp SQLite file
├── CleanArchGrpcService.Architecture.Tests/   # Layering rules (ArchUnitNET)
└── CleanArchGrpcService.Functional.Tests/     # RPC round-trip (GrpcChannel + WebApplicationFactory)
```

## 🧱 Layers

**Domain** depends on nothing but the language itself.

- `Entity`: base type, identity-based equality
- `AggregateRoot : Entity`, adding a `DomainEvents` collection; only the aggregate can raise its own events
- `TodoItem.Create(title)` guards against an empty/whitespace title and raises `TodoItemCreatedEvent`

**Application** depends only on `Domain` and the mediator's contracts (`IRequest`, `IRequestHandler`, `ISender`); identical in shape to `webapi`'s Application layer.

```csharp
public sealed record CreateTodoItemCommand(string Title) : IRequest<Guid>;

public sealed class CreateTodoItemCommandHandler : IRequestHandler<CreateTodoItemCommand, Guid>
{
    private readonly ITodoItemRepository _repository;

    public CreateTodoItemCommandHandler(ITodoItemRepository repository) => _repository = repository;

    public async Task<Guid> Handle(CreateTodoItemCommand request, CancellationToken ct)
    {
        var todoItem = TodoItem.Create(request.Title);
        await _repository.AddAsync(todoItem, ct);
        return todoItem.Id;
    }
}
```

Validators (FluentValidation) are auto-discovered by assembly and run in a `ValidationBehavior` pipeline step before the handler.

**Infrastructure** implements the ports `Application` defines (`ITodoItemRepository`) and depends only on `Application`.

**Grpc** hosts the service and depends only on `Application`:

```csharp
public sealed class TodoGrpcService(ISender sender) : TodoService.TodoServiceBase
{
    public override async Task<CreateTodoItemResponse> CreateTodoItem(
        CreateTodoItemRequest request,
        ServerCallContext context)
    {
        var id = await sender.Send(new CreateTodoItemCommand(request.Title), context.CancellationToken);
        return new CreateTodoItemResponse { Id = id.ToString() };
    }
}
```

gRPC has no HTTP middleware pipeline equivalent to `webapi`'s `ValidationExceptionHandler`, so validation failures surface through a `Grpc.Core.Interceptors.Interceptor` instead: `ValidationInterceptor` catches `FluentValidation.ValidationException` and translates it into an `RpcException(StatusCode.InvalidArgument, detail)`.

## 🧪 Testing

| Project | Verifies | Database | Docker |
|---|---|---|---|
| `Application.Tests` | Handlers, validators, behaviors, domain entities | None | No |
| `Integration.Tests` | Real EF Core persistence via `Database.MigrateAsync()` | SQLite (temp file) | No |
| `Architecture.Tests` | Layers don't leak into each other (ArchUnitNET) | N/A | No |
| `Functional.Tests` | RPC round-trip via `GrpcChannel` against `WebApplicationFactory<Program>` | SQLite (temp file) | No |

No test tier touches Docker: the stack is fixed at SQLite, so there's no container-backed provider to spin up.

## ⚙️ Configuration

There is none. Unlike `webapi`, this template has no `--database`, `--orm`, or `--orchestrator` flag: the stack is fixed at SQLite + EF Core + Aspire.

| Parameter | Default | Values |
|---|---|---|
| `IncludeTests` | `true` | Whether the four test projects above were generated |

## ⌨️ CLI commands

If `Dorn.Cli` is available (globally, or as the local tool this project's `.config/dotnet-tools.json` already pins), these run from the project root:

```bash
dorn test              # all 4 tiers, or dorn test --tier <name> for one
dorn run                # Aspire, auto-detected
dorn coverage           # tests + coverage, gated at 80%
```

## 📚 Learn more

Generated by [Dorn](https://github.com/mbarretot/dorn), a .NET scaffolding CLI. No CI workflow yet (`webapi`'s depends on a provider/orchestrator matrix this fixed MVP doesn't have). See the [`grpc` template reference](https://github.com/mbarretot/dorn/blob/main/docs/templates/grpc.md) and [architecture decision records](https://github.com/mbarretot/dorn/tree/main/docs/adr) for the reasoning behind these choices.
