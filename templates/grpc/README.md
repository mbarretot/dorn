# Template: `grpc`

Clean Architecture gRPC service with CQRS, EF Core (SQLite), and Aspire orchestration.

## Structure

```
src/
├── ProjectName.Domain/            # Entities, domain events, no dependencies
├── ProjectName.Application/       # Commands, queries, handlers, validators, behaviors
├── ProjectName.Infrastructure/    # EF Core DbContext, migrations
├── ProjectName.Grpc/              # Proto contract, gRPC services, Program.cs
├── ProjectName.AppHost/           # Aspire AppHost — registers the service as a project resource
└── ProjectName.ServiceDefaults/   # Aspire ServiceDefaults — telemetry, health checks, resilience
tests/
├── ProjectName.Application.Tests/   # Unit: handlers, provider-agnostic
├── ProjectName.Integration.Tests/   # Integration: EF Core against a temp SQLite file
├── ProjectName.Architecture.Tests/  # Architecture: layering rules (ArchUnitNET)
└── ProjectName.Functional.Tests/    # Functional: GrpcChannel round-trip via WebApplicationFactory
```

Unlike `templates/webapi`, this template has no `--orm`, `--database`, or `--orchestrator`
choice: the MVP scope is fixed at sqlite + EF Core + Aspire. The only generation parameter is
`IncludeTests`.

## Layers

### Domain

Language-only dependencies (plus `Dorn.SharedKernel`/`Dorn.Messaging.Contracts`, resolved as
NuGet packages, not copied). No references to EF Core, gRPC, or Application-layer libraries.

- `Entity` — base type with `Id` and identity-based equality
- `AggregateRoot` — extends `Entity` with a `DomainEvents` collection
- `TodoItem` — the worked example aggregate; `Create(title)` guards against an empty/whitespace
  title and raises `TodoItemCreatedEvent`

### Application

Pure business logic. Depends only on `Domain` and the custom mediator contracts (`IRequest`,
`IRequestHandler`, `ISender`).

- **Commands/Queries** — records implementing `IRequest<T>` or `IRequest`
- **Handlers** — implement `IRequestHandler<TRequest, TResponse>`, dispatch through
  `ITodoItemRepository`
- **Validators** — FluentValidation, auto-discovered by assembly
- **Behaviors** — cross-cutting pipeline (validation, logging, etc.)

### Infrastructure

Implements the ports defined in `Domain`/`Application`. Depends only on `Application`.

- `ApplicationDbContext` — EF Core DbContext
- `Repositories/EfCore/TodoItemRepository.cs` — implements `ITodoItemRepository`
- SQLite migrations (flat `Persistence/Migrations/`, no provider subfolder)

### Grpc

Hosts the gRPC service. Depends only on `Application` and `Infrastructure`.

```csharp
public sealed class TodoGrpcService(ISender sender) : TodoService.TodoServiceBase
{
    public override async Task<CreateTodoItemResponse> CreateTodoItem(
        CreateTodoItemRequest request,
        ServerCallContext context)
    {
        var id = await sender.Send(
            new CreateTodoItemCommand(request.Title),
            context.CancellationToken);

        return new CreateTodoItemResponse { Id = id.ToString() };
    }
}
```

Validation failures surface as `RpcException` with `StatusCode.InvalidArgument` via a
`Grpc.Core.Interceptors.Interceptor` — gRPC has no HTTP middleware pipeline equivalent to
webapi's `ValidationExceptionHandler`.

## CQRS with the mediator

Commands and queries are records. Handlers receive only what they need through the
constructor.

```csharp
// Command
public sealed record CreateTodoItemCommand(string Title) : IRequest<Guid>;

// Handler
public sealed class CreateTodoItemCommandHandler : IRequestHandler<CreateTodoItemCommand, Guid>
{
    private readonly ITodoItemRepository _repository;

    public CreateTodoItemCommandHandler(ITodoItemRepository repository) =>
        _repository = repository;

    public async Task<Guid> Handle(CreateTodoItemCommand request, CancellationToken ct)
    {
        var todoItem = TodoItem.Create(request.Title);
        await _repository.AddAsync(todoItem, ct);
        return todoItem.Id;
    }
}
```

## Domain events

Only `AggregateRoot` can raise events.

```csharp
public class TodoItem : AggregateRoot
{
    public string Title { get; private set; } = string.Empty;
    public bool IsComplete { get; private set; }

    public static TodoItem Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title must not be empty.", nameof(title));
        }

        var todoItem = new TodoItem { Title = title };
        todoItem.AddDomainEvent(new TodoItemCreatedEvent(todoItem.Id, todoItem.Title));
        return todoItem;
    }
}
```

## Validation

FluentValidation validators auto-registered via `AddValidatorsFromAssembly`. A
`ValidationInterceptor` catches `FluentValidation.ValidationException` and translates it into
an `RpcException(StatusCode.InvalidArgument, detail)`, where `detail` identifies the failing
field(s).

## Testing strategy

Four tiers, each with a distinct goal:

| Project              | Goal                                                                    | Database                  |
| --------------------- | ------------------------------------------------------------------------ | -------------------------- |
| `Application.Tests`  | Unit — handlers, validators, behaviors, domain entities                | No database                |
| `Integration.Tests`  | Real persistence against EF Core / SQLite, via `Database.MigrateAsync()` | SQLite file                |
| `Architecture.Tests` | Fitness functions: Domain/Application/Infrastructure do not leak across layers | —                     |
| `Functional.Tests`   | Round-trip RPC via `GrpcChannel` against `WebApplicationFactory<Program>` | SQLite (temp file)         |

See `docs/adr/0013-four-tier-test-strategy.md` for the rationale behind this split.

## Aspire

The generated `AppHost` registers the service as a project resource over HTTP/2:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
builder.AddProject<Projects.ProjectName_Grpc>("grpc");
builder.Build().Run();
```

Run `dotnet dev-certs https --trust` once, then `dotnet run` against the `AppHost` project.

## Options

| Parameter      | Default | Description               |
| -------------- | ------- | -------------------------- |
| `IncludeTests` | `true`  | Include the test projects  |
