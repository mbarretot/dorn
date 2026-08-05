# CleanArchWebApi

[![Scaffolded with Dorn](https://img.shields.io/badge/scaffolded_with-Dorn-1A1A1A?style=flat-square)](https://github.com/mbarretot/dorn)

A Clean Architecture Web API — Domain, Application, Infrastructure, and WebApi fully wired, CQRS via a custom mediator, and a database provider/ORM combination fixed at generation time.

## 🚀 Getting started

```bash
dotnet build
dotnet run --project src/CleanArchWebApi.WebApi
```

If this project was generated with `--orchestrator aspire` (the default), run it through the AppHost instead so Aspire's dashboard and service discovery are wired up:

```bash
dotnet run --project src/CleanArchWebApi.AppHost
```

> [!TIP]
> `dorn test`, `dorn run`, and `dorn coverage` also work from this project's root — see [CLI commands](#cli-commands) below.

## 📁 Project structure

```
src/
├── CleanArchWebApi.Domain/            # Entities, domain events — no dependencies
├── CleanArchWebApi.Application/       # Commands, queries, handlers, validators, behaviors
├── CleanArchWebApi.Infrastructure/    # EF Core or Dapper implementations, migrations
└── CleanArchWebApi.WebApi/            # Minimal API endpoints, Program.cs
tests/
├── CleanArchWebApi.Application.Tests/    # Unit: handlers, validators, behaviors
├── CleanArchWebApi.Integration.Tests/    # Real persistence against the chosen DatabaseProvider
├── CleanArchWebApi.Architecture.Tests/   # Layering rules (ArchUnitNET)
└── CleanArchWebApi.Functional.Tests/     # HTTP end-to-end (WebApplicationFactory)
```

`--orchestrator aspire` additionally generates `CleanArchWebApi.AppHost/` and `CleanArchWebApi.ServiceDefaults/`.

## 🧱 Layers

**Domain** depends on nothing but the language itself.

- `Entity` — base type, identity-based equality
- `AggregateRoot : Entity` — adds a `DomainEvents` collection; only the aggregate can raise its own events
- `Result` — success/failure without exceptions

**Application** depends only on `Domain` and the mediator's contracts (`IRequest`, `IRequestHandler`, `ISender`).

```csharp
public sealed record CreateTodoItemCommand(string Title) : IRequest<Guid>;

public sealed class CreateTodoItemCommandHandler : IRequestHandler<CreateTodoItemCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public CreateTodoItemCommandHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<Guid> Handle(CreateTodoItemCommand request, CancellationToken ct)
    {
        var todoItem = TodoItem.Create(request.Title);
        _dbContext.Items.Add(todoItem);
        await _dbContext.SaveChangesAsync(ct);
        return todoItem.Id;
    }
}
```

Validators (FluentValidation) are auto-discovered by assembly and run in a `ValidationBehavior` pipeline step before the handler. Domain events raised via `AddDomainEvent` dispatch automatically inside `SaveChangesAsync` — only `AggregateRoot` can raise them.

**Infrastructure** implements the ports `Application` defines (`IApplicationDbContext`, repository interfaces) and depends only on `Application`. Its EF Core and Dapper implementations live side by side under `Repositories/EfCore/` and `Repositories/Dapper/`; only the one selected at generation time is included.

**WebApi** hosts the Minimal API and depends only on `Application`:

```csharp
var group = app.MapGroup("/api/todos").WithTags("Todos");

group.MapPost("/", async (CreateTodoItemCommand command, ISender sender, CancellationToken ct) =>
{
    var id = await sender.Send(command, ct);
    return Results.Created($"/api/todos/{id}", id);
});
```

## 🧪 Testing

| Project | Verifies | Database | Docker |
|---|---|---|---|
| `Application.Tests` | Handlers, validators, behaviors | SQLite in-memory | No |
| `Integration.Tests` | Real persistence against the chosen `DatabaseProvider` | SQLite file, or a real SQL Server/PostgreSQL via Testcontainers | Only with `--database sqlserver`/`postgres` |
| `Architecture.Tests` | Layers don't leak into each other (ArchUnitNET) | — | No |
| `Functional.Tests` | HTTP round-trip via `WebApplicationFactory<Program>` | SQLite, forced regardless of provider | No |

With the default `--database sqlite`, no test tier touches Docker. `Functional.Tests` always forces SQLite — its job is the HTTP pipeline, not provider fidelity, which `Integration.Tests` already covers.

## ⚙️ Configuration

These were generation-time choices — change them by regenerating, not by editing this project:

| Parameter | Default | Values |
|---|---|---|
| `Orm` | `efcore` | `efcore` (migrations, change tracking) or `dapper` (raw SQL, no tracking) |
| `DatabaseProvider` | `sqlite` | `sqlite` (zero-config), `sqlserver`, or `postgres` (both via an Aspire-managed container) |
| `Orchestrator` | `aspire` | `aspire`, `docker-compose`, or `none` |
| `IncludeTests` | `true` | Whether the four test projects above were generated |

## ⌨️ CLI commands

If `Dorn.Cli` is available (globally, or as the local tool this project's `.config/dotnet-tools.json` already pins), these run from the project root:

```bash
dorn test              # all 4 tiers — or dorn test --tier <name> for one
dorn run                # Aspire / Docker Compose / plain dotnet run, auto-detected
dorn coverage           # tests + coverage, gated at 80%
```

## 🔄 CI

`.github/workflows/ci.yml` and a pinned `global.json` ship with every generation, ready from the first push.

- **Triggers**: `push`, `pull_request`, and manual `workflow_dispatch` (with an `exclude_tiers` input to skip specific tiers). No `schedule`, no path filters.
- **Matrix**: `os` (`ubuntu-latest`, `windows-latest`) × `orchestrator` (`aspire`, `docker-compose`, `none`) — six cells. The database provider isn't a matrix axis; a `configuration` job reads it from a `.github/config/db-provider.txt` marker before the matrix starts.
- **SQL Server/PostgreSQL on Windows are best-effort**: `windows-latest` runners have no Docker host, so `Integration.Tests` can't start a container there. The Linux cell runs a real container (`azure-sql-edge` / `postgres`) with a health check before testing; Windows is a documented caveat, not a bug.

## 📚 Learn more

This project was generated by [Dorn](https://github.com/mbarretot/dorn), a .NET scaffolding CLI. See its [`webapi` template reference](https://github.com/mbarretot/dorn/blob/main/docs/templates/webapi.md) and [architecture decision records](https://github.com/mbarretot/dorn/tree/main/docs/adr) for the reasoning behind these choices.
