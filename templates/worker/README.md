# CleanArchWorkerService

[![Scaffolded with Dorn](https://img.shields.io/badge/scaffolded_with-Dorn-1A1A1A?style=flat-square)](https://github.com/mbarretot/dorn)

A Clean Architecture background worker: Domain, Application, Infrastructure, and a `BackgroundService` presentation layer fully wired, CQRS via a custom mediator, on a fixed SQLite + EF Core + Aspire stack.

## 🚀 Getting started

```bash
dotnet run --project src/CleanArchWorkerService.AppHost
```

Aspire isn't optional here: there's no `--orchestrator` flag, so the AppHost is the only supported way to run this service.

> [!TIP]
> `dorn test`, `dorn run`, and `dorn coverage` also work from this project's root; see [CLI commands](#cli-commands) below.

## 📁 Project structure

```
src/
├── CleanArchWorkerService.Domain/            # Entities, domain events, no dependencies
├── CleanArchWorkerService.Application/       # Commands, queries, handlers, validators, behaviors
├── CleanArchWorkerService.Infrastructure/    # EF Core DbContext, SQLite migration
├── CleanArchWorkerService.Worker/            # TodoProcessingWorker (BackgroundService), Program.cs
├── CleanArchWorkerService.AppHost/           # Aspire AppHost: registers the service as a resource
└── CleanArchWorkerService.ServiceDefaults/   # Aspire ServiceDefaults: telemetry, health checks, resilience
tests/
├── CleanArchWorkerService.Application.Tests/    # Unit: handlers, validators, behaviors
├── CleanArchWorkerService.Integration.Tests/    # Real EF Core persistence against a temp SQLite file
├── CleanArchWorkerService.Architecture.Tests/   # Layering rules (ArchUnitNET)
└── CleanArchWorkerService.Functional.Tests/     # Hosted-service loop, against a real IHost
```

## 🧱 Layers

**Domain** depends on nothing but the language itself.

- `Entity`: base type, identity-based equality
- `AggregateRoot : Entity`, adding a `DomainEvents` collection; only the aggregate can raise its own events
- `TodoItem.Create(title)` guards against an empty/whitespace title and raises `TodoItemCreatedEvent`
- `TodoItem.MarkComplete()` is idempotent: it sets `IsComplete` and raises `TodoItemCompletedEvent` once; a repeated call raises nothing

**Application** depends only on `Domain` and the mediator's contracts (`IRequest`, `IRequestHandler`, `ISender`); identical in shape to `webapi`'s Application layer.

```csharp
public sealed record ProcessPendingTodoItemsCommand : IRequest<int>;

public sealed class ProcessPendingTodoItemsCommandHandler(ITodoItemRepository repository)
    : IRequestHandler<ProcessPendingTodoItemsCommand, int>
{
    public async Task<int> Handle(ProcessPendingTodoItemsCommand request, CancellationToken ct)
    {
        var pending = await repository.GetPendingAsync(ct);
        foreach (var item in pending) item.MarkComplete();
        await repository.SaveChangesAsync(ct);
        return pending.Count;
    }
}
```

Validators (FluentValidation) are auto-discovered by assembly and run in a `ValidationBehavior` pipeline step before the handler.

**Infrastructure** implements the ports `Application` defines (`ITodoItemRepository`) and depends only on `Application`.

**Worker** hosts the loop and depends only on `Application`:

```csharp
public sealed class TodoProcessingWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<WorkerOptions> options,
    ILogger<TodoProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessOnceAsync(stoppingToken);
        }
    }
}
```

A `BackgroundService` is a singleton, but `ISender`/`ApplicationDbContext`/`ITodoItemRepository` are scoped — each tick resolves its own `IServiceScopeFactory.CreateAsyncScope()` rather than capturing a single `DbContext` for the process lifetime. There is no HTTP or gRPC surface to speak of: `MapDefaultEndpoints()` (`/health`, `/alive`) is the only mapped endpoint, kept so the Aspire dashboard can report the worker healthy.

## 🧪 Testing

| Project | Verifies | Database | Docker |
|---|---|---|---|
| `Application.Tests` | Handlers, validators, behaviors, domain entities | None | No |
| `Integration.Tests` | Real EF Core persistence via `Database.MigrateAsync()` | SQLite (temp file) | No |
| `Architecture.Tests` | Layers don't leak into each other (ArchUnitNET) | N/A | No |
| `Functional.Tests` | Hosted-service loop against a real `IHost`, driven by `TimeProvider` | SQLite (temp file) | No |

No test tier touches Docker: the stack is fixed at SQLite, so there's no container-backed provider to spin up.

## ⚙️ Configuration

There is no `--database`, `--orm`, or `--orchestrator` flag: the stack is fixed at SQLite + EF Core + Aspire.

| Parameter | Default | Values |
|---|---|---|
| `IncludeTests` | `true` | Whether the four test projects above were generated |

At runtime, `Worker:Interval` in `appsettings.json` controls how often the loop ticks (default `00:00:30`).

## ⌨️ CLI commands

If `Dorn.Cli` is available (globally, or as the local tool this project's `.config/dotnet-tools.json` already pins), these run from the project root:

```bash
dorn test              # all 4 tiers, or dorn test --tier <name> for one
dorn run                # Aspire, auto-detected
dorn coverage           # tests + coverage, gated at 80%
```

## 📚 Learn more

Generated by [Dorn](https://github.com/mbarretot/dorn), a .NET scaffolding CLI. No CI workflow yet (`webapi`'s depends on a provider/orchestrator matrix this fixed MVP doesn't have). See the [`worker` template reference](https://github.com/mbarretot/dorn/blob/main/docs/templates/worker.md) and [architecture decision records](https://github.com/mbarretot/dorn/tree/main/docs/adr) for the reasoning behind these choices.
