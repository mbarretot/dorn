# Template: `webapi`

The `webapi` template (short name `dorn-webapi`, identity `Dorn.Templates.WebApi`)
generates an ASP.NET Core Minimal API project in Clean Architecture, using a
from-scratch, MIT-licensed CQRS mediator (no MediatR) and EF Core + SQLite persistence.

```bash
dorn new webapi MyApp
# or, from a repo checkout during development:
dotnet run --project src/Dorn.Cli -- new webapi MyApp
```

This creates `./MyApp/` (override with `-o|--output`; pass `--force` to overwrite a
non-empty directory), sourced from `Dorn.Templates.WebApi` and renamed from the
template's `sourceName` (`CleanArchWebApi`) to your project name throughout files,
folders, and namespaces.

### Alternative: vanilla `dotnet new`, without the `dorn` CLI

`templates/webapi` is also distributed as a standalone NuGet template package
(`<PackageType>Template</PackageType>`), installable with plain `dotnet new` and requiring
no `dorn` tool at all. This is the same mechanism Visual Studio's "Create a new project"
search uses to discover third-party templates.

```bash
# Build the package locally (not yet published to NuGet.org — see below):
pwsh eng/scripts/pack-templates.ps1

# Install it, then generate a project exactly like `dorn new webapi` would:
dotnet new install ./artifacts/Dorn.Templates.WebApi.*.nupkg
dotnet new dorn-webapi -n MyApp

# Remove it when you're done:
dotnet new uninstall Dorn.Templates.WebApi
```

This path is completely independent of the `dorn` CLI — it uses the global
`~/.templateengine` cache that `dotnet new install`/`uninstall` manage, separate from the
isolated host the `dorn` CLI uses under `~/.dorn/template-engine`. Both paths generate
from the exact same `templates/webapi/` content.

`Dorn.Templates.WebApi` isn't published to NuGet.org yet (same TODO status as the `dorn`
CLI's own NuGet publishing — see `eng/README.md`), so for now `dotnet new install` above
points at a locally built `.nupkg` under `./artifacts/`. Once published,
`dotnet new install Dorn.Templates.WebApi` will work directly by package ID, without
building anything locally first. See `docs/adr/0009-dual-distribution-dotnet-new-template-pack.md`
for the full decision record.

## Layers

The generated solution (`<Name>.sln`, itself self-contained with its own
`Directory.Build.props`/`Directory.Packages.props` — see `docs/architecture.md`) has four
projects under `src/`:

- **`<Name>.Domain`** — entities and domain primitives. Includes `Entity` (base type
  providing an `Id` and identity-based equality), `AggregateRoot : Entity` (adds the
  domain-event collection, with `AddDomainEvent` restricted to `protected` and
  `ClearDomainEvents` public — only an aggregate can raise its own events), `INotification`
  (the marker interface domain events implement), and `Result` (a lightweight result type
  for representing success/failure without exceptions), plus template-specific entities
  such as `TodoItem`. `Entity`, `AggregateRoot`, and `Result` come from the
  `Dorn.SharedKernel` NuGet package; `INotification` comes from `Dorn.Messaging.Contracts`
  — see ADR 0011.
- **`<Name>.Application`** — CQRS commands/queries, handlers, and application-layer ports
  such as `IApplicationDbContext` that `Infrastructure` implements. The mediator itself
  (`IRequest`, `ISender`, `IRequestHandler<,>`, etc.) comes from the `Dorn.Messaging.Contracts`
  and `Dorn.Messaging` NuGet packages, not a local `Messaging/` folder — see ADR 0011. No
  dependency on EF Core directly — only on the `IApplicationDbContext` abstraction it
  defines.
- **`<Name>.Infrastructure`** — EF Core `DbContext` implementing `IApplicationDbContext`,
  and `AddInfrastructure(this IServiceCollection, IConfiguration)` which registers the
  `DbContext` (SQLite provider, connection string from configuration) and binds
  `IApplicationDbContext` to it.
- **`<Name>.WebApi`** — the ASP.NET Core host: Minimal API endpoints (via `MapGroup`,
  see below), `Program.cs` composition root, `appsettings.json`.

Plus, conditionally, `tests/<Name>.Application.Tests` — an xUnit + NSubstitute test
project for the Application layer.

## The `IncludeTests` parameter

```bash
dorn new webapi MyApp                    # tests/ included (default)
dotnet new dorn-webapi -n MyApp --IncludeTests false   # via raw dotnet new, tests/ excluded
```

`IncludeTests` is a boolean template parameter (`.template.config/template.json`,
default `true`) that controls whether `tests/<Name>.Application.Tests/` is generated at
all. Dorn's own CLI (`dorn new webapi`) does not currently expose a flag for this — it's
reachable today via `dotnet new dorn-webapi` directly against the template once
discovered by the Template Engine, or by editing the generated output afterward. Exposing
it through `dorn new webapi` is open for contribution (see
`src/Dorn.Cli/Commands/New/NewWebApiSettings.cs`/`NewWebApiCommand.cs` for where
`GenerationRequest.Parameters` would need to be populated from a new CLI option).

## CQRS with the custom mediator

Requests are records implementing `IRequest<TResponse>`; handlers implement
`IRequestHandler<TRequest, TResponse>`; endpoints depend only on `ISender`. Example, from
the generated `Todos` feature:

```csharp
// Application/Todos/CreateTodoItem/CreateTodoItemCommand.cs
public sealed record CreateTodoItemCommand(string Title) : IRequest<Guid>;

// Application/Todos/CreateTodoItem/CreateTodoItemCommandHandler.cs
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

Wired to a Minimal API endpoint with `MapGroup`:

```csharp
// WebApi/Endpoints/TodoEndpoints.cs
var group = app.MapGroup("/api/todos").WithTags("Todos");

group.MapPost("/", async (CreateTodoItemCommand command, ISender sender, CancellationToken ct) =>
{
    var id = await sender.Send(command, ct);
    return Results.Created($"/api/todos/{id}", id);
});
```

Handlers (and any `IPipelineBehavior<,>` implementations you add for cross-cutting
concerns like validation or logging) are discovered and registered by a single call in
`Program.cs`:

```csharp
builder.Services.AddMediator(typeof(CreateTodoItemCommand).Assembly);
```

See `docs/architecture.md` and `docs/adr/0003-custom-mediator-instead-of-mediatr.md` for
why this is a from-scratch mediator instead of MediatR.

## Domain events with `INotification`

Only aggregate roots (`AggregateRoot`, not plain `Entity`) raise domain events.
`INotification` is a marker interface that comes from the `Dorn.Messaging.Contracts`
NuGet package: `AggregateRoot.DomainEvents` is typed `IReadOnlyCollection<INotification>`,
and `AggregateRoot` (from `Dorn.SharedKernel`) depends on `Dorn.Messaging.Contracts` for
that one type — the same dependency-free contracts package `INotificationHandler<T>` and
`IPublisher` reference. See ADR 0011 for why `INotification` lives in
`Dorn.Messaging.Contracts` rather than `Dorn.SharedKernel`.

An aggregate raises an event from within its own method, using the `protected`
`AddDomainEvent`:

```csharp
// Domain/Entities/TodoItem.cs
public class TodoItem : AggregateRoot
{
    public string Title { get; private set; } = string.Empty;

    public bool IsComplete { get; private set; }

    private TodoItem() { }

    public static TodoItem Create(string title)
    {
        var todoItem = new TodoItem { Title = title };
        todoItem.AddDomainEvent(new TodoItemCreatedEvent(todoItem.Id, todoItem.Title));
        return todoItem;
    }
}

// Domain/Events/TodoItemCreatedEvent.cs
public sealed record TodoItemCreatedEvent(Guid TodoItemId, string Title) : INotification;
```

`ApplicationDbContext.SaveChangesAsync` dispatches pending events after a successful save,
then clears them, so an event is never published for a transaction that didn't actually
commit:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    var aggregatesWithEvents = ChangeTracker
        .Entries<AggregateRoot>()
        .Select(entry => entry.Entity)
        .Where(aggregate => aggregate.DomainEvents.Count > 0)
        .ToList();

    var result = await base.SaveChangesAsync(cancellationToken);

    foreach (var aggregate in aggregatesWithEvents)
    {
        var domainEvents = aggregate.DomainEvents.ToArray();
        aggregate.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }
    }

    return result;
}
```

`INotificationHandler<TNotification>` implementations subscribe to an event type — zero,
one, or many per event type, all of them invoked on `Publish`. They're auto-registered by
the same `AddMediator` assembly scan that registers `IRequestHandler<,>` and
`IPipelineBehavior<,>` implementations, no separate registration call needed:

```csharp
// Application/Todos/CreateTodoItem/TodoItemCreatedEventHandler.cs
public sealed class TodoItemCreatedEventHandler : INotificationHandler<TodoItemCreatedEvent>
{
    private readonly ILogger<TodoItemCreatedEventHandler> _logger;

    public TodoItemCreatedEventHandler(ILogger<TodoItemCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(TodoItemCreatedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "Todo item {TodoItemId} created: {Title}",
            notification.TodoItemId,
            notification.Title
        );
        return Task.CompletedTask;
    }
}
```

See `docs/adr/0010-ddd-aggregates-and-domain-events.md` for the full decision record,
including why dispatch is sequential and in-process rather than an outbox or a
fire-and-forget strategy.

## Persistence: EF Core + SQLite by default

`Infrastructure/Persistence/ApplicationDbContext.cs` is a plain `DbContext` implementing
the `Application`-layer `IApplicationDbContext` port. The default provider is SQLite,
configured in `Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(configuration.GetConnectionString("Default")));
```

with the connection string in `appsettings.json`:

```json
"ConnectionStrings": { "Default": "Data Source=app.db" }
```

SQLite is zero-config — a generated project builds and runs without installing or
provisioning a database server, which matters for a scaffolded starting point. The
template ships a real EF Core migration (`Infrastructure/Persistence/Migrations/`), and
`Program.cs` calls `dbContext.Database.MigrateAsync()` on startup, so `dotnet run` against
a freshly generated project creates `app.db` and its schema automatically — no manual
`dotnet ef database update` step needed for the golden path. This was verified by
generating a project, running it, and exercising `POST`/`GET /api/todos` for real.

To swap to SQL Server or PostgreSQL:

1. Replace the `Microsoft.EntityFrameworkCore.Sqlite` package reference (and its
   `PackageVersion` entry in `templates/webapi`'s — or your generated project's —
   `Directory.Packages.props`) with `Microsoft.EntityFrameworkCore.SqlServer` or
   `Npgsql.EntityFrameworkCore.PostgreSQL`.
2. Change `options.UseSqlite(...)` to `options.UseSqlServer(...)` /
   `options.UseNpgsql(...)` in `AddInfrastructure`.
3. Update the `Default` connection string in `appsettings.json` (and
   `appsettings.Development.json` if you add one) to match the new provider.
4. Delete `Infrastructure/Persistence/Migrations/` and regenerate it for the new provider
   (`dotnet ef migrations add InitialCreate --project src/<Name>.Infrastructure
   --startup-project src/<Name>.WebApi`) — EF Core migrations are provider-specific and
   the SQLite ones won't apply cleanly to SQL Server/PostgreSQL.

See `docs/adr/0005-ef-core-sqlite-default-persistence.md` for the full rationale.
