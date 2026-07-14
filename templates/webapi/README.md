# Template: `webapi`

Clean Architecture Minimal API con CQRS, EF Core y soporte para SQLite o SQL Server.

## Estructura

```
src/
├── ProjectName.Domain/            # Entidades, eventos de dominio, sin dependencias
├── ProjectName.Application/       # Commands, queries, handlers, Validators, behaviors
├── ProjectName.Infrastructure/    # EF Core DbContext, migraciones
└── ProjectName.WebApi/           # Minimal API endpoints, Program.cs
tests/
└── ProjectName.Application.Tests/  # xUnit + Nsubstitute
```

Con `--orchestrator aspire` se agrega `ProjectName.AppHost/` y `ProjectName.ServiceDefaults/`.

## Capas

### Domain

Solo dependencias del lenguaje. Sin referencias a EF Core, frameworks ni librerias de aplicacion.

- `Entity` — base con `Id` e igualdad basada en identidad
- `AggregateRoot` — extiende `Entity` + coleccion de `DomainEvents`
- `Result` — resultado sin excepciones para success/failure

### Application

Logica de negocio pura. Solo depende de `Domain` y de los contratos del mediator (`IRequest`, `IRequestHandler`, `ISender`).

- **Commands/Queries** — records que implementan `IRequest<T>` o `IRequest`
- **Handlers** — implementan `IRequestHandler<TRequest, TResponse>`
- **Validators** — FluentValidation, auto-descubiertos por assembly
- **Behaviors** — pipeline de cross-cutting (validacion, logging, etc.)

### Infrastructure

Implementa los puertos definidos en `Application`. Solo depende de `Application`.

- `ApplicationDbContext` — DbContext que implementa `IApplicationDbContext`
- Migraciones EF Core (SQLite o SQL Server segun `--database`)

### WebApi

Hosts la Minimal API. Solo depende de `Application`.

```csharp
var group = app.MapGroup("/api/todos").WithTags("Todos");

group.MapPost("/", async (CreateTodoItemCommand command, ISender sender, CancellationToken ct) =>
{
    var id = await sender.Send(command, ct);
    return Results.Created($"/api/todos/{id}", id);
});

group.MapGet("/", async (ISender sender, CancellationToken ct) =>
{
    var items = await sender.Send(new GetTodoItemsQuery(), ct);
    return Results.Ok(items);
});
```

## CQRS con el mediator

Commands y queries son records. Handlers solo reciben lo que necesitan (el DbContext via constructor, no el request completo).

```csharp
// Command
public sealed record CreateTodoItemCommand(string Title) : IRequest<Guid>;

// Handler
public sealed class CreateTodoItemCommandHandler : IRequestHandler<CreateTodoItemCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public CreateTodoItemCommandHandler(IApplicationDbContext dbContext) =>
        _dbContext = dbContext;

    public async Task<Guid> Handle(CreateTodoItemCommand request, CancellationToken ct)
    {
        var todoItem = new TodoItem { Title = request.Title };
        _dbContext.Items.Add(todoItem);
        await _dbContext.SaveChangesAsync(ct);
        return todoItem.Id;
    }
}
```

## Eventos de dominio

Solo `AggregateRoot` puede generar eventos. Se dispatchean automaticamente en `SaveChangesAsync`.

```csharp
public class TodoItem : AggregateRoot
{
    public string Title { get; private set; }
    public bool IsComplete { get; private set; }

    public static TodoItem Create(string title)
    {
        var item = new TodoItem { Title = title };
        item.AddDomainEvent(new TodoItemCreatedEvent(item.Id, item.Title));
        return item;
    }
}
```

```csharp
public sealed class TodoItemCreatedEventHandler : INotificationHandler<TodoItemCreatedEvent>
{
    // puede disparar integrations events, logging, etc.
}
```

## Validacion

Validators de FluentValidation auto-registrados via `AddValidatorsFromAssembly`. El pipeline de `ValidationBehavior` los ejecuta antes del handler.

```csharp
public sealed class CreateTodoItemCommandValidator : AbstractValidator<CreateTodoItemCommand>
{
    public CreateTodoItemCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);
    }
}
```

## Options

| Parametro | Default | Descripcion |
|---|---|---|
| `DatabaseProvider` | `sqlite` | `sqlite` (zero-config) o `sqlserver` (Aspire container) |
| `Orchestrator` | `aspire` | `aspire` (AppHost) o `docker-compose` |
| `IncludeTests` | `true` | Incluir proyecto de tests |
