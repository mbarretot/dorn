# Template: `webapi`

## Contents

- [Alternative: vanilla `dotnet new`, without the `dorn` CLI](#alternative-vanilla-dotnet-new-without-the-dorn-cli)
- [Layers](#layers)
- [AppHost & ServiceDefaults](#apphost--servicedefaults)
- [Orchestration: Aspire vs. Docker Compose vs. None](#orchestration-aspire-vs-docker-compose-vs-none)
- [The `IncludeTests` parameter](#the-includetests-parameter)
- [Running the generated project: `dorn test`, `dorn run`, `dorn coverage`](#running-the-generated-project-dorn-test-dorn-run-dorn-coverage)
- [Local tool manifest](#local-tool-manifest)
- [Code formatting](#code-formatting)
- [Continuous Integration](#continuous-integration)
- [CQRS with the custom mediator](#cqrs-with-the-custom-mediator)
- [Domain events with `INotification`](#domain-events-with-inotification)
- [Persistence: EF Core, database provider selection](#persistence-ef-core-database-provider-selection)

The `webapi` template (short name `dorn-webapi`, identity `Dorn.Templates.WebApi`)
generates an ASP.NET Core Minimal API project in Clean Architecture, using a
from-scratch, MIT-licensed CQRS mediator (no MediatR) and EF Core persistence, with a
database provider chosen at generation time.

```bash
dorn new webapi MyApp                             # SQLite (default), no external setup required
dorn new webapi MyApp --database sqlserver        # SQL Server, run via an Aspire-managed container
dorn new webapi MyApp --database postgres         # PostgreSQL, run via an Aspire-managed container
dorn new webapi MyApp --orchestrator docker-compose  # Docker Compose scaffolding, no Aspire dependency
dorn new webapi MyApp --orchestrator none         # no orchestration scaffolding, run the API directly
dorn new webapi MyApp                             # omit --database/--orchestrator in an interactive terminal to be prompted
# or, from a repo checkout during development:
dotnet run --project src/Dorn.Cli -- new webapi MyApp
```

This creates `./MyApp/` (`-o|--output` to override; `--force` to overwrite a non-empty
directory), sourced from `Dorn.Templates.WebApi` and renamed from the template's
`sourceName` (`CleanArchWebApi`) to your project name throughout files, folders, and
namespaces.

Full `--database` behavior: [Persistence: EF Core, database provider
selection](#persistence-ef-core-database-provider-selection) below
(`docs/adr/0011-database-provider-selection.md`). Full `--orchestrator` behavior:
[Orchestration: Aspire vs. Docker Compose vs.
None](#orchestration-aspire-vs-docker-compose-vs-none).

## Alternative: vanilla `dotnet new`, without the `dorn` CLI

`templates/webapi` is also distributed as a standalone NuGet template package
(`<PackageType>Template</PackageType>`), installable with plain `dotnet new`, requiring
no `dorn` tool at all.

```bash
# Install it, then generate a project exactly like `dorn new webapi` would:
dotnet new install Dorn.Templates.WebApi
dotnet new dorn-webapi -n MyApp

# Remove it when you're done:
dotnet new uninstall Dorn.Templates.WebApi
```

Completely independent of the `dorn` CLI: it uses the global `~/.templateengine` cache
(managed by `dotnet new install`/`uninstall`), separate from the isolated
`~/.dorn/template-engine` host `dorn` uses. Both paths generate from the same
`templates/webapi/` content.

`Dorn.Templates.WebApi` is published as version `1.0.0` on NuGet, installable directly by
package ID. Contributors testing unpublished changes can instead run
`pwsh eng/scripts/pack-templates.ps1` and install
`./artifacts/Dorn.Templates.WebApi.*.nupkg`. See
`docs/adr/0008-dual-distribution-dotnet-new-template-pack.md`.

[⬆ Back to top](#contents)

## Layers

The generated solution (`<Name>.slnx`, self-contained with its own
`Directory.Build.props`/`Directory.Packages.props`; see `docs/architecture.md`) has four
projects under `src/`:

- **`<Name>.Domain`**: entities and domain primitives: `Entity` (base type, `Id` +
  identity-based equality), `AggregateRoot : Entity` (adds the domain-event collection;
  see [Domain events](#domain-events-with-inotification)), `INotification` (marker
  interface for domain events), and `Result` (success/failure without exceptions), plus
  template-specific entities like `TodoItem`. `Entity`, `AggregateRoot`, and `Result`
  come from `Dorn.SharedKernel`; `INotification` comes from `Dorn.Messaging.Contracts`
  (ADR 0010).
- **`<Name>.Application`**: CQRS commands/queries, handlers, and application-layer ports
  such as `IApplicationDbContext` that `Infrastructure` implements. The mediator itself
  (`IRequest`, `ISender`, `IRequestHandler<,>`, etc.) comes from the
  `Dorn.Messaging.Contracts` and `Dorn.Messaging` packages, not a local `Messaging/`
  folder (ADR 0010). No dependency on EF Core directly, only on the
  `IApplicationDbContext` abstraction it defines.
- **`<Name>.Infrastructure`**: EF Core `DbContext` implementing `IApplicationDbContext`,
  and `AddInfrastructure(this IServiceCollection, IConfiguration)` which registers the
  `DbContext` (SQLite or SQL Server, chosen at generation time; see below) and binds
  `IApplicationDbContext` to it.
- **`<Name>.WebApi`**: the ASP.NET Core host: Minimal API endpoints (via `MapGroup`, see
  below), `Program.cs` composition root, `appsettings.json`.

Plus, conditionally, `tests/<Name>.Application.Tests`: an xUnit + NSubstitute test
project for the Application layer.

[⬆ Back to top](#contents)

## AppHost & ServiceDefaults

Generated only when `--orchestrator aspire` (the default); see
[Orchestration](#orchestration-aspire-vs-docker-compose-vs-none) below for the
`docker-compose`/`none` alternatives. The solution includes a standard .NET Aspire
orchestration layer, generated by `dotnet new aspire-apphost`/`aspire-servicedefaults`
and wired into the template:

- **`<Name>.AppHost`**: orchestrates local runs. `dotnet run --project src/<Name>.AppHost`
  starts the Aspire dashboard and launches the `<Name>.WebApi` resource under it. With
  the default SQLite provider (an embedded file-based DB Aspire doesn't
  containerize/orchestrate), the AppHost only orchestrates the WebApi project itself.
  With `--database sqlserver`/`postgres`, it additionally provisions a matching container
  resource (`builder.AddSqlServer(...)` or `builder.AddPostgres(...)`) and wires its
  connection string into WebApi via `WithReference(...)`; this requires Docker running
  locally. See [Persistence](#persistence-ef-core-database-provider-selection).
- **`<Name>.ServiceDefaults`**: a shared class library centralizing OpenTelemetry
  (logging, metrics, tracing; OTLP exporter enabled when `OTEL_EXPORTER_OTLP_ENDPOINT` is
  set), health checks, and service-discovery/resilience defaults for outgoing
  `HttpClient`s. Consumed from `Program.cs` via `builder.AddServiceDefaults()` (before
  other service registrations) and `app.MapDefaultEndpoints()` (`/health` and `/alive`,
  `Development` only).

## Orchestration: Aspire vs. Docker Compose vs. None

`--orchestrator` is chosen independently of `--database`: the two axes compose freely.
All three `--orchestrator` values are covered by `templates/tests`, each paired with at
least one `--database` value (not every cell of the full 3×2 matrix has a dedicated
generation test, since the two axes are otherwise orthogonal).

| `--orchestrator` value | Default | Local run | `AppHost`/`ServiceDefaults` generated | `docker-compose.yml` generated | `<Name>.slnx` includes `AppHost`/`ServiceDefaults` |
|---|---|---|---|---|---|
| `aspire` | Yes | `dotnet run --project src/<Name>.AppHost` | Yes | No | Yes |
| `docker-compose` | No | `docker compose up --build` | No | Yes | No |
| `none` | No | `dotnet run --project src/<Name>.WebApi` | No | No | No |

`Dockerfile` and `.dockerignore` are generated unconditionally on all three paths (only
`docker-compose.yml` references the Dockerfile).

- **`aspire`** (default): see [AppHost & ServiceDefaults](#apphost--servicedefaults).
- **`docker-compose`** and **`none`**: no `src/<Name>.AppHost`/`ServiceDefaults`,
  `<Name>.WebApi.csproj` has no `ServiceDefaults` reference, and `Program.cs` skips
  `AddServiceDefaults()`/`MapDefaultEndpoints()`. The generated `<Name>.slnx` lists only
  `Application`, `Domain`, `Infrastructure`, `WebApi`, and `Application.Tests`.

`docker-compose` additionally generates:

- **`Dockerfile`** (`src/<Name>.WebApi/Dockerfile`): a multi-stage build (`sdk:10.0` →
  `aspnet:10.0`) restoring/publishing `<Name>.WebApi.csproj`. Generated for all three
  orchestrators, but only referenced by `docker-compose.yml` on the compose path.
- **`.dockerignore`**: always generated, keeps `bin/`/`obj/`/`.git`/docs out of the build
  context.
- **`docker-compose.yml`**: a `webapi` service built from the Dockerfile
  (`ports: 8080:8080`), run with `docker compose up --build`.
  - `--database sqlserver`: adds a `sqlserver` service (image
    `mcr.microsoft.com/mssql/server:2022-latest`, healthcheck, named volume) and a
    `ConnectionStrings__<Name>` override on `webapi` pointing at the `sqlserver` DNS name
    with `TrustServerCertificate=true`.
  - `--database postgres`: same pattern with a `postgres` service (image `postgres:17`,
    `pg_isready` healthcheck, named volume) and a matching connection string.
  - Both mirror what Aspire's `WithReference(...)` injects at runtime on the Aspire path.
- `otel-collector` in `docker-compose.yml` is a **commented-out placeholder only**: no
  OpenTelemetry is wired into WebApi on the compose path.

`none` is the minimal path: no Aspire, no Compose scaffolding. Unlike `docker-compose`,
none of `docker-compose.yml`, `docker-compose.SqlServer.yml`, or
`docker-compose.Postgres.yml` is generated. `Dockerfile` and `.dockerignore` still
generate, so `docker build` remains possible without Compose. Run directly with
`dotnet run --project src/<Name>.WebApi`.

Omit `--orchestrator` in an interactive terminal to be prompted ("Aspire" / "Docker
Compose" / "None (run directly)"; underlying values `aspire`, `docker-compose`, `none`);
a non-interactive session falls back to `aspire`.

Generated projects reference the published `Dorn.Messaging`, `Dorn.Messaging.Contracts`,
and `Dorn.SharedKernel` packages at version `1.0.0`, so end-user builds restore them from
NuGet without this repo's local `./artifacts` feed (a contributor-only workflow for
unpublished package changes).

[⬆ Back to top](#contents)

## The `IncludeTests` parameter

```bash
dorn new webapi MyApp                    # tests/ included (default)
dotnet new dorn-webapi -n MyApp --IncludeTests false   # via raw dotnet new, tests/ excluded
```

`IncludeTests` is a boolean template parameter (`.template.config/template.json`,
default `true`) controlling whether `tests/<Name>.Application.Tests/` is generated at
all. `dorn new webapi` doesn't currently expose a flag for this; reach it via
`dotnet new dorn-webapi` directly against the template. Exposing it through
`dorn new webapi` is open for contribution (see
`src/Dorn.Cli/Commands/New/NewWebApiSettings.cs`/`NewWebApiCommand.cs` for where
`GenerationRequest.Parameters` would need a new CLI option).

[⬆ Back to top](#contents)

## Running the generated project: `dorn test`, `dorn run`, `dorn coverage`

Generated webapi projects ship three convenience verbs that work from the project root
with no extra setup beyond the local-tool restore below, auto-detecting project layout
and the right `dotnet test` filter/orchestrator for your generation-time choices.

```bash
dorn test              # runs all 4 tiers (Application / Integration / Architecture / Functional)
dorn test --tier unit  # one tier only; also: integration, architecture, functional
dorn run               # picks AppHost → Aspire, else docker-compose.yml → Compose, else plain `dotnet run`
dorn coverage          # runs tests with coverage, applies the fixed 80% threshold gate
```

All three accept `--project <path>` (default: CWD), working identically from inside the
generated project or a parent directory.

`dorn new webapi` runs `dotnet tool restore` automatically after generation (skip with
`--no-restore`) so `dotnet dorn test` (local-tool resolution, not PATH) works
immediately with identical behavior.

[⬆ Back to top](#contents)

## Local tool manifest

Every generated webapi project ships `.config/dotnet-tools.json` pinning `dorn.cli` at
the same `1.0.1` token the rest of the `Dorn.*` packages use, enabling
`dotnet dorn <verb>` without a global tool install:

```bash
cd MyApp
dotnet tool restore    # one-time per clone (dorn new webapi does this automatically)
dotnet dorn test       # equivalent to `dorn test` from any directory on PATH
```

If installed via plain `dotnet new install` (no `dorn` CLI), run `dotnet tool restore`
manually; `dorn new webapi` only runs it on its own code path.

The manifest is pinned (`rollForward: false`) so a generated project won't silently float
to a newer major `dorn.cli` version. Upgrade by editing `.config/dotnet-tools.json` and
re-running `dotnet tool restore`.

[⬆ Back to top](#contents)

## Code formatting

The generated project ships a `.editorconfig`: the single source of truth for layout,
`var`, expression-bodied, `using`, and naming conventions. Respected by Visual Studio,
Rider, and VS Code (enable format-on-save), and applied from the command line with
SDK-native `dotnet format` (no install, no tool manifest):

```bash
dotnet format                       # format the whole solution in place
dotnet format --verify-no-changes   # check only; non-zero exit if anything is unformatted
```

`dotnet format` reads `.editorconfig` directly: no second, conflicting formatter config
to keep in sync. EF Core migration files under
`src/<Name>.Infrastructure/Persistence/Migrations/` are marked `generated_code = true`
and left untouched. No build-time, git-hook, or CI enforcement; `dotnet format` is
opt-in.

[⬆ Back to top](#contents)

## Continuous Integration

Every generated project ships a working `.github/workflows/ci.yml`, plus a static
`global.json` pinning the same .NET SDK version dorn itself builds against
(`setup-dotnet`'s `global-json-file` step reads it). Both generated unconditionally; no
flag controls them. See `docs/adr/0013-scaffolded-ci-workflow.md`.

- **Triggers**: `push`, `pull_request`, and manual `workflow_dispatch` with one optional
  input, `exclude_tiers` (comma-separated tier names). No `schedule`, no path filters.
- **Matrix**: six cells: `os` (`ubuntu-latest`, `windows-latest`) × `orchestrator`
  (`aspire`, `docker-compose`, `none`). Database provider isn't a matrix axis: a
  generated repository only ever contains the one provider chosen at `dorn new webapi`
  time, tracked by a committed `.github/config/db-provider.txt` marker a `configuration`
  job reads before the matrix starts.
- **Test execution**: one solution-wide `dotnet test` per cell by default;
  `exclude_tiers` on a manual run switches to one `dotnet test` per non-excluded tier
  project (`Application`, `Integration`, `Functional`, `Architecture`) instead.
- **Database-provider conditional steps** (Linux runners only, gated on
  `db-provider.txt`): `sqlserver` and `postgres` markers each add two steps, "Start
  <Provider> (Linux)" (`docker run mcr.microsoft.com/azure-sql-edge` or
  `docker run postgres:17`), then "Wait for <Provider> to be healthy (Linux)" (polls
  `sqlcmd -Q "select 1"` or `pg_isready -U postgres`).
  - Both generate their disposable container password at CI runtime via
    `openssl rand -base64 24`, never a literal committed value.
  - Neither pair runs for `sqlite`, or on Windows (see caveat below).
  - Plain `if:`-gated steps, not a `services:` block: GitHub Actions service containers
    don't support a per-service `if:` and only start on Linux runners.
- **Windows + SQL Server/PostgreSQL is best-effort**: `windows-latest` has no Docker
  host, so `Integration.Tests`' `PersistenceTestFixture` (ADR 0012) can't start SQL
  Server/PostgreSQL there; documented on the matching step, and the Windows cell still
  runs the non-database-dependent tiers.
- **Coverage**: `dotnet test --collect:"XPlat Code Coverage"` on every cell; a
  ReportGenerator aggregation step runs only on `ubuntu-latest` (`Html;Cobertura;Badges`,
  excluding `*.Tests` assemblies).
- **Out of scope**: no `dotnet ef`, `dotnet pack`, `dotnet nuget push`, Dependabot, or
  badge automation; `actionlint` is intentionally omitted (dorn's own CI doesn't run it
  either).

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

[⬆ Back to top](#contents)

## Domain events with `INotification`

Only aggregate roots (`AggregateRoot`, not plain `Entity`) raise domain events.
`INotification` is a marker interface that comes from the `Dorn.Messaging.Contracts`
package: `AggregateRoot.DomainEvents` is typed `IReadOnlyCollection<INotification>`, and
`AggregateRoot` (from `Dorn.SharedKernel`) depends on `Dorn.Messaging.Contracts` for that
one type, the same dependency-free contracts package `INotificationHandler<T>` and
`IPublisher` reference. See ADR 0010 for why `INotification` lives in
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

`ApplicationDbContext.SaveChangesAsync` dispatches pending events after a successful
save, then clears them, so an event is never published for a transaction that didn't
actually commit:

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

`INotificationHandler<TNotification>` implementations subscribe to an event type: zero,
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

See `docs/adr/0009-ddd-aggregates-and-domain-events.md` for why dispatch is sequential
and in-process rather than an outbox or a fire-and-forget strategy.

[⬆ Back to top](#contents)

## Persistence: EF Core, database provider selection

`Infrastructure/Persistence/ApplicationDbContext.cs` is a plain `DbContext` implementing
the `Application`-layer `IApplicationDbContext` port. The provider is chosen at
generation time via `dorn new webapi MyApp --database sqlite|sqlserver|postgres`:

| `--database` value | Setup required | Behavior |
|---|---|---|
| `sqlite` (default) | None | Zero-config: builds and runs without installing or provisioning a database server. |
| `sqlserver` | Docker | Runs SQL Server via an Aspire-managed container; no manual server provisioning. |
| `postgres` | Docker | Runs PostgreSQL via an Aspire-managed container, at parity with the SQL Server path. |

Omit `--database` in an interactive terminal to be prompted; a non-interactive session
(e.g. CI) falls back to `sqlite`.

```csharp
// Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
services.AddDbContext<ApplicationDbContext>(options =>
#if (UseSqlite)
    options.UseSqlite(configuration.GetConnectionString("Default"))
#elif (UseSqlServer)
    options.UseSqlServer(configuration.GetConnectionString("CleanArchWebApi"))
#elif (UsePostgres)
    options.UseNpgsql(configuration.GetConnectionString("CleanArchWebApi"))
#endif
);
```

With SQLite, the connection string is static in `appsettings.json`
(`"ConnectionStrings": { "Default": "Data Source=app.db" }`). With SQL Server or
PostgreSQL, no static connection string is needed: Aspire's `WithReference(...)` in
`AppHost.cs` injects it into the WebApi project's configuration at runtime under the
resource name `"CleanArchWebApi"` (renamed to your project name like everything else
sourced from `sourceName`).

The template ships a real, provider-specific EF Core migration for whichever provider is
selected (`Infrastructure/Persistence/Migrations/`, generated once per provider, so
there's exactly one `ApplicationDbContextModelSnapshot`), and `Program.cs` calls
`dbContext.Database.MigrateAsync()` on startup: `dotnet run` (SQLite) or
`dotnet run --project src/<Name>.AppHost` (SQL Server/PostgreSQL, Docker running)
creates the schema automatically, no manual `dotnet ef database update` needed. Verified
by generating a project with each provider, building it, and exercising
`POST`/`GET /api/todos` for real.

For other, unsupported engines (MySQL, Oracle), the same manual swap the SQL Server and
PostgreSQL providers used to require still applies:

1. Replace the provider-specific EF Core package reference (and its `PackageVersion`
   entry in `Directory.Packages.props`) with the target provider's EF Core package.
2. Change the `options.Use...(...)` call in `AddInfrastructure` to the target provider's
   equivalent.
3. Add or update the `ConnectionStrings` entry in `appsettings.json`
   (`appsettings.Development.json` too, if used); starting from
   `--database sqlserver`/`postgres` there's no static entry, since Aspire injects it at
   runtime, so add one.
4. If starting from `--database sqlserver`/`postgres`, remove the matching Aspire
   container wiring (the `builder.Add...(...)` resource and `.WithReference(...)` in
   `AppHost.cs`, and the `Aspire.Hosting.*` package reference in
   `<Name>.AppHost.csproj`), or you're left running an unused container.
5. Delete `Infrastructure/Persistence/Migrations/` and regenerate for the new provider
   (`dotnet ef migrations add InitialCreate --project src/<Name>.Infrastructure
   --startup-project src/<Name>.WebApi`): migrations are provider-specific and none of
   the existing ones apply cleanly to a different engine.

See `docs/adr/0005-ef-core-sqlite-default-persistence.md` (original SQLite-only
rationale), `docs/adr/0011-database-provider-selection.md` (SQL Server as a first-class,
Aspire-hosted choice), and `docs/adr/0014-postgresql-database-provider.md` (PostgreSQL at
the same parity).

[⬆ Back to top](#contents)
