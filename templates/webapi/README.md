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
├── ProjectName.Application.Tests/    # Unit: handlers, provider-agnostic (SQLite in-memory)
├── ProjectName.Integration.Tests/    # Integration: real DatabaseProvider (Testcontainers para SQL Server)
├── ProjectName.Architecture.Tests/   # Architecture: reglas de layering (NetArchTest.Rules)
└── ProjectName.Functional.Tests/     # Functional: WebApplicationFactory, HTTP end-to-end
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

## Estrategia de testing

Cuatro niveles, cada uno con un objetivo distinto:

| Proyecto             | Objetivo                                                                              | Base de datos                                | Docker                                           |
| -------------------- | ------------------------------------------------------------------------------------- | -------------------------------------------- | ------------------------------------------------ |
| `Application.Tests`  | Unit — handlers, validators, behaviors, rapido                                        | SQLite in-memory (`EnsureCreated`)           | No                                               |
| `Integration.Tests`  | Persistencia real contra el `DatabaseProvider` elegido, via `Database.MigrateAsync()` | SQLite archivo o SQL Server real             | Solo con `--database sqlserver` (Testcontainers) |
| `Architecture.Tests` | Fitness functions: Domain/Application/Infrastructure no se filtran entre capas        | —                                            | No                                               |
| `Functional.Tests`   | Round-trip HTTP real via `WebApplicationFactory<Program>`                             | SQLite (forzado, independiente del provider) | No                                               |

`Integration.Tests` es el unico nivel que puede requerir Docker, y solo cuando el proyecto
se genero con `--database sqlserver`: usa `Testcontainers.MsSql` para levantar un SQL Server
real y correr las migraciones EF Core reales contra el, en vez de `EnsureCreated()`. Con
`--database sqlite` (default) no hay Docker involucrado en ningun nivel.

`Functional.Tests` fuerza SQLite en un archivo temporal unico, sin importar el
`DatabaseProvider` elegido — su objetivo es probar el pipeline HTTP (routing, validacion,
serializacion), no la fidelidad del provider, que ya cubre `Integration.Tests`.

Ver `docs/adr/0013-four-tier-test-strategy.md` para el detalle de esta decision.

## Options

| Parametro          | Default  | Descripcion                                             |
| ------------------ | -------- | ------------------------------------------------------- |
| `DatabaseProvider` | `sqlite` | `sqlite` (zero-config) o `sqlserver` (Aspire container) |
| `Orchestrator`     | `aspire` | `aspire` (AppHost) o `docker-compose`                   |
| `IncludeTests`     | `true`   | Incluir proyecto de tests                               |
