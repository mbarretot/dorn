# Template: `webapi`

The `webapi` template (short name `dorn-webapi`, identity `Dorn.Templates.WebApi`)
generates an ASP.NET Core Minimal API project in Clean Architecture, using a
from-scratch, MIT-licensed CQRS mediator (no MediatR) and EF Core persistence, with a
choice of database provider at generation time.

```bash
dorn new webapi MyApp                             # SQLite (default), no external setup required
dorn new webapi MyApp --database sqlserver        # SQL Server, run via an Aspire-managed container
dorn new webapi MyApp --orchestrator docker-compose  # Docker Compose scaffolding, no Aspire dependency
dorn new webapi MyApp                             # omit --database/--orchestrator in an interactive terminal to be prompted
# or, from a repo checkout during development:
dotnet run --project src/Dorn.Cli -- new webapi MyApp
```

This creates `./MyApp/` (override with `-o|--output`; pass `--force` to overwrite a
non-empty directory), sourced from `Dorn.Templates.WebApi` and renamed from the
template's `sourceName` (`CleanArchWebApi`) to your project name throughout files,
folders, and namespaces.

See [Persistence: EF Core, database provider selection](#persistence-ef-core-database-provider-selection)
below for the full `--database` behavior (and
`docs/adr/0012-database-provider-selection.md` for that decision record), and
[Orchestration: Aspire vs. Docker Compose](#orchestration-aspire-vs-docker-compose) for the
full `--orchestrator` behavior.

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

The generated solution (`<Name>.slnx`, itself self-contained with its own
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
  `DbContext` (SQLite or SQL Server, chosen at generation time — see below) and binds
  `IApplicationDbContext` to it.
- **`<Name>.WebApi`** — the ASP.NET Core host: Minimal API endpoints (via `MapGroup`,
  see below), `Program.cs` composition root, `appsettings.json`.

Plus, conditionally, `tests/<Name>.Application.Tests` — an xUnit + NSubstitute test
project for the Application layer.

## AppHost & ServiceDefaults

Generated only when `--orchestrator aspire` (the default) — see
[Orchestration: Aspire vs. Docker Compose](#orchestration-aspire-vs-docker-compose) below for
the `docker-compose` alternative. The solution includes a standard .NET Aspire orchestration
layer, generated by `dotnet new aspire-apphost` / `aspire-servicedefaults` and wired into the
template:

- **`<Name>.AppHost`** — orchestrates local runs. `dotnet run --project src/<Name>.AppHost`
  starts the Aspire dashboard and launches the `<Name>.WebApi` resource under it. With the
  default SQLite provider, SQLite stays untouched by Aspire's resource model (it's an
  embedded file-based DB, not something Aspire containerizes/orchestrates) — the AppHost
  only orchestrates the WebApi project itself. With `--database sqlserver`, the AppHost
  additionally provisions a SQL Server container resource (`builder.AddSqlServer(...)`)
  and wires its connection string into the WebApi project via `WithReference(...)` — this
  requires Docker to be running locally. See
  [Persistence: EF Core, database provider selection](#persistence-ef-core-database-provider-selection).
- **`<Name>.ServiceDefaults`** — a shared class library centralizing OpenTelemetry
  (logging, metrics, tracing, with an OTLP exporter enabled when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set), health checks, and service-discovery/resilience
  defaults for outgoing `HttpClient`s. Consumed from `Program.cs` via
  `builder.AddServiceDefaults()` (before other service registrations) and
  `app.MapDefaultEndpoints()` (which maps `/health` and `/alive`, only in `Development`).

## Orchestration: Aspire vs. Docker Compose

`--orchestrator` is chosen independently of `--database` — the two axes compose freely, so all
four combinations (`aspire`/`docker-compose` x `sqlite`/`sqlserver`) are supported and covered
by `tests/Templates.Tests`.

- **`--orchestrator aspire`** (default) — see
  [AppHost & ServiceDefaults](#apphost--servicedefaults) above. Local runs go through
  `dotnet run --project src/<Name>.AppHost`.
- **`--orchestrator docker-compose`** — no Aspire dependency at all: `src/<Name>.AppHost` and
  `src/<Name>.ServiceDefaults` are not generated, `<Name>.WebApi.csproj` has no
  `ServiceDefaults` reference, and `Program.cs` doesn't call `AddServiceDefaults()` /
  `MapDefaultEndpoints()`. Instead, the template root gets:
  - **`Dockerfile`** (`src/<Name>.WebApi/Dockerfile`) — a multi-stage build
    (`sdk:10.0` → `aspnet:10.0`) that restores/publishes `<Name>.WebApi.csproj`, build context
    is the generated project root. Generated for **both** orchestrators (the Aspire path can
    also `docker build` its WebApi image), but only referenced by `docker-compose.yml` on the
    compose path.
  - **`.dockerignore`** — always generated, keeps `bin/`/`obj/`/`.git`/docs out of the build
    context.
  - **`docker-compose.yml`** — a `webapi` service built from the Dockerfile
    (`ports: 8080:8080`). With `--database sqlserver`, an additional `sqlserver` service (image
    `mcr.microsoft.com/mssql/server:2022-latest`, with a healthcheck and a named volume) is
    included, and the `webapi` service gets a `ConnectionStrings__<Name>` environment override
    pointing at the `sqlserver` compose DNS name with `TrustServerCertificate=true` — the
    compose-path equivalent of what Aspire's `WithReference(sql)` injects at runtime on the
    Aspire path. Run with `docker compose up --build`.
  - The generated `<Name>.slnx` on this path lists only `Application`, `Domain`,
    `Infrastructure`, `WebApi`, and `Application.Tests` — no `AppHost`/`ServiceDefaults`
    entries, since those projects don't exist in this generation.
  - The `otel-collector` service in `docker-compose.yml` is a **commented-out placeholder
    only** — there is no OpenTelemetry instrumentation wired into the WebApi project on the
    compose path (that parity with the Aspire path is out of scope for now).

Omit `--orchestrator` in an interactive terminal to be prompted (labeled "Aspire" /
"Docker Compose" in the prompt; the underlying value passed to the template engine is the
kebab-case `docker-compose`); a non-interactive session falls back to `aspire`.

**Known limitation, not introduced by `--orchestrator docker-compose`:** a generated project's
`Dorn.Messaging`/`Dorn.Messaging.Contracts`/`Dorn.SharedKernel` package references resolve from
a local dev NuGet feed, not NuGet.org yet (see ADR 0011,
`docs/adr/0011-extract-messaging-and-shared-kernel-as-nuget-packages.md`). Plain
`docker build`/`docker compose build` by an end user outside this repo checkout will fail to
restore those packages until they're published — the exact same limitation the host
`dotnet build` already has today. Containerization doesn't fix or work around this; it's
tracked as a pre-existing gap in ADR 0011 and `eng/README.md`'s TODO list.

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

## Code formatting

The generated project ships a `.editorconfig` that is the single source of truth for
layout, `var`, expression-bodied, `using`, and naming conventions. It's already
respected by Visual Studio, Rider, and VS Code (enable format-on-save), and can be
applied from the command line with the SDK-native `dotnet format` (no install, no tool
manifest):

    dotnet format                    # format the whole solution in place
    dotnet format --verify-no-changes  # check only; non-zero exit if anything is unformatted

`dotnet format` reads `.editorconfig` directly, so there is no second, conflicting
formatter config to keep in sync. EF Core migration files under
`src/<Name>.Infrastructure/Persistence/Migrations/` are marked `generated_code = true`
and are left untouched. No build-time, git-hook, or CI enforcement is wired up — running
`dotnet format` is opt-in and up to you.

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

## Persistence: EF Core, database provider selection

`Infrastructure/Persistence/ApplicationDbContext.cs` is a plain `DbContext` implementing
the `Application`-layer `IApplicationDbContext` port. The provider is chosen at
generation time via `dorn new webapi MyApp --database sqlite|sqlserver`:

- **`--database sqlite`** (default) — zero-config: a generated project builds and runs
  without installing or provisioning a database server, which matters for a scaffolded
  starting point.
- **`--database sqlserver`** — runs SQL Server via an Aspire-managed container, so it's
  runnable out of the box with Docker instead of requiring a manually provisioned server.
- Omit `--database` in an interactive terminal to be prompted; in a non-interactive
  session (e.g. CI) the omitted flag silently falls back to `sqlite`.

```csharp
// Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
services.AddDbContext<ApplicationDbContext>(options =>
#if (UseSqlServer)
    options.UseSqlServer(configuration.GetConnectionString("CleanArchWebApi"))
#else
    options.UseSqlite(configuration.GetConnectionString("Default"))
#endif
);
```

With SQLite, the connection string is static in `appsettings.json`
(`"ConnectionStrings": { "Default": "Data Source=app.db" }`). With SQL Server, no static
connection string is needed — Aspire's `WithReference(sql)` in `AppHost.cs` injects the
resolved connection string into the WebApi project's configuration at runtime under the
resource name `"CleanArchWebApi"` (renamed to your project name like everything else
sourced from `sourceName`).

The template ships a real, provider-specific EF Core migration for whichever provider is
selected (`Infrastructure/Persistence/Migrations/`, generated once per provider — SQLite's
and SQL Server's authoring folders never both land in the same generated output, so there
is exactly one `ApplicationDbContextModelSnapshot`), and `Program.cs` calls
`dbContext.Database.MigrateAsync()` on startup, so `dotnet run` (SQLite) or
`dotnet run --project src/<Name>.AppHost` (SQL Server, with Docker running) against a
freshly generated project creates the schema automatically — no manual
`dotnet ef database update` step needed for the golden path. This was verified by
generating a project with each provider, building it, and exercising
`POST`/`GET /api/todos` for real.

To swap to PostgreSQL (not a first-class `--database` choice, still a manual swap):

1. Replace the `Microsoft.EntityFrameworkCore.Sqlite`/`.SqlServer` package reference (and
   its `PackageVersion` entry in `templates/webapi`'s — or your generated project's —
   `Directory.Packages.props`) with `Npgsql.EntityFrameworkCore.PostgreSQL`.
2. Change `options.UseSqlite(...)`/`options.UseSqlServer(...)` to `options.UseNpgsql(...)`
   in `AddInfrastructure`.
3. Add or update the `ConnectionStrings` entry in `appsettings.json` (and
   `appsettings.Development.json` if you add one) to match the new provider — if you're
   starting from `--database sqlserver`, there is no static entry to update, since Aspire
   injects that connection string at runtime; you'll need to add one.
4. If you started from `--database sqlserver`, remove the Aspire SQL Server wiring: the
   `builder.AddSqlServer("sql").AddDatabase(...)` resource and `.WithReference(sql)` in
   `AppHost.cs`, and the `Aspire.Hosting.SqlServer` package reference in
   `<Name>.AppHost.csproj` — otherwise you're left running an unused SQL Server container.
5. Delete `Infrastructure/Persistence/Migrations/` and regenerate it for the new provider
   (`dotnet ef migrations add InitialCreate --project src/<Name>.Infrastructure
   --startup-project src/<Name>.WebApi`) — EF Core migrations are provider-specific and
   neither the SQLite nor the SQL Server ones will apply cleanly to PostgreSQL.

See `docs/adr/0005-ef-core-sqlite-default-persistence.md` for the original SQLite-only
rationale, and `docs/adr/0012-database-provider-selection.md` for the decision to make
SQL Server a first-class, Aspire-hosted `--database` choice.
