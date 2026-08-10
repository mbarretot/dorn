# Template: `worker`

The `worker` template (short name `dorn-worker`, identity `Dorn.Templates.Worker`)
generates a background/scheduled service in Clean Architecture, using the same
from-scratch CQRS mediator as `webapi` and `grpc`, and EF Core over SQLite. Like `grpc`,
it has a fixed MVP scope: no `--database`, `--orm`, or `--orchestrator` flags.

```bash
dorn new worker MyWorker
dotnet run --project src/MyWorker.AppHost   # starts the Aspire dashboard + the worker
```

This creates `./MyWorker/` (`-o|--output` to override; `--force` overwrites a non-empty
directory), sourced from `Dorn.Templates.Worker` and renamed from the template's
`sourceName` (`CleanArchWorkerService`) to your project name throughout. `dorn new worker`
runs `dotnet tool restore` automatically after generation (skip with `--no-restore`),
same as `webapi`/`grpc`; see [Local tool manifest](./webapi.md#local-tool-manifest).

## Scope: a fixed MVP, not a smaller `webapi`

`webapi` lets you choose a database provider, ORM, and orchestrator at generation time
(see [Persistence](./webapi.md#persistence-ef-core-database-provider-selection) and
[Orchestration](./webapi.md#orchestration-aspire-vs-docker-compose-vs-none)). `worker`
collapses all three axes to one fixed combination (SQLite + EF Core + Aspire), no flags,
the same posture `grpc` takes (see [`grpc`'s scope
section](./grpc.md#scope-a-fixed-mvp-not-a-smaller-webapi)):

```bash
dorn new worker MyWorker                 # SQLite + EF Core + Aspire, the only combination
dorn new worker MyWorker -o ./out        # custom output directory
dorn new worker MyWorker --force         # overwrite a non-empty directory
dorn new worker MyWorker --no-restore    # skip the post-generation `dotnet tool restore`
```

`NewWorkerCommand`/`NewWorkerSettings` (`src/Dorn.Cli/Commands/New/`) accept only
`<name>`, `-o|--output`, `--force`, and `--no-restore`: no interactive
provider/ORM/orchestrator prompt, and no `--trigger` flag either — the trigger is a
timer, not a choice. See [ADR 0018](../adr/0018-worker-template-scoped-mvp.md).

## Why a background worker is hosted by `WebApplication`

`<Name>.Worker/Program.cs` calls `WebApplication.CreateBuilder(args)`, not
`Host.CreateApplicationBuilder(args)`, even though the worker exposes no API. This is
deliberate, not a copy-paste leftover from `webapi`/`grpc`:

- `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` are Aspire's health
  probe (`/health`, `/alive`). Aspire's dashboard polls a resource's health endpoint to
  decide whether to mark it "Running"; a plain `IHost` built via
  `Host.CreateApplicationBuilder` has no `MapDefaultEndpoints()` to call
  (`AddServiceDefaults`/`MapDefaultEndpoints` are `IHostApplicationBuilder`/
  `WebApplication` extensions), so the Aspire dashboard would show the worker
  perpetually "Starting" even while it's ticking correctly.
- `/health` and `/alive` are the **only** mapped endpoints. There is no `MapGet`, no
  OpenAPI, no `MapGrpcService` — the Kestrel port that comes along with
  `WebApplication.CreateBuilder` is otherwise unused.
- Unlike `grpc`, there is **no** `Kestrel:EndpointDefaults:Protocols` override in
  `appsettings.json`. That entry existed in `grpc` solely to pin ALPN for HTTP/2 gRPC
  calls alongside HTTP/1.1 health checks on the same port; a worker makes no RPC calls,
  so nothing needs it.

If you generate a worker and see an idle-looking Kestrel endpoint in the logs, this is
why: it exists only so Aspire can see the worker is alive, not to serve traffic.

## Layers

The generated solution (`<Name>.slnx`, self-contained with its own
`Directory.Build.props`/`Directory.Packages.props`) has six projects under `src/`:

- **`<Name>.Domain`**: entities and domain primitives. `TodoItem` (an `AggregateRoot`)
  with `TodoItemCreatedEvent` and `TodoItemCompletedEvent`, structurally close to
  `webapi`'s Domain layer plus one method, `MarkComplete()`; see
  [Domain events with `INotification`](./webapi.md#domain-events-with-inotification).
- **`<Name>.Application`**: CQRS commands/queries, handlers, and `ValidationBehavior`.
  Depends only on `Domain` and the `Dorn.Messaging.Contracts`/`Dorn.Messaging` packages
  (ADR 0010). No dependency on EF Core or `Microsoft.Extensions.Hosting`.
- **`<Name>.Infrastructure`**: EF Core `DbContext`, a flat SQLite migration, and
  `TodoItemRepository` (adds `GetPendingAsync` over `webapi`'s repository).
- **`<Name>.Worker`**: the presentation layer, if a background service can be said to
  have one: `Program.cs`, `WorkerOptions`, `TodoProcessingWorker` (the
  `BackgroundService`), and `AddWorker(...)` (`DependencyInjection/ServiceCollectionExtensions.cs`).
  Equivalent in role to `webapi`'s `.WebApi` project or `grpc`'s `.Grpc` project, but
  driven by a timer instead of an inbound request.
- **`<Name>.AppHost`** / **`<Name>.ServiceDefaults`**: the same Aspire layer `webapi`
  ships with `--orchestrator aspire` (see
  [AppHost & ServiceDefaults](./webapi.md#apphost--servicedefaults)); always generated
  here.

Plus, conditionally (`IncludeTests`, default `true`), four test projects under `tests/`:
`<Name>.Application.Tests`, `<Name>.Integration.Tests`, `<Name>.Architecture.Tests`, and
`<Name>.Functional.Tests`.

## The example flow: `ProcessPendingTodoItems`

`webapi`/`grpc` demonstrate their CQRS pipeline with an inbound `CreateTodoItem`
request. A worker has no inbound request, so `worker` demonstrates the same pipeline
from a timer tick instead — and, deliberately, with a **write**, not a read: proving the
loop can only run a query would leave the domain-event pipeline unexercised.

```
PeriodicTimer(WorkerOptions.Interval, TimeProvider)      "Worker:Interval", default 00:00:30
   │ WaitForNextTickAsync(stoppingToken)
   ▼
TodoProcessingWorker.ProcessOnceAsync   (singleton BackgroundService)
   │
   ├─ IServiceScopeFactory.CreateAsyncScope()             one scope per tick, see below
   │     └─ ISender (scoped) ──▶ ProcessPendingTodoItemsCommand
   │            └─ ValidationBehavior<,> ──▶ handler
   │                   └─ ITodoItemRepository.GetPendingAsync()   (WHERE !IsComplete)
   │                   └─ TodoItem.MarkComplete() ──▶ AddDomainEvent(TodoItemCompletedEvent)
   │                   └─ SaveChangesAsync()
   │                          ├─ base.SaveChangesAsync() → SQLite commit
   │                          └─ IPublisher.Publish(event) → TodoItemCompletedEventHandler (logs)
   ├─ catch (Exception) → LogError, loop survives the next interval
   └─ scope disposed (await using)
```

`TodoProcessingWorker` (`src/CleanArchWorkerService.Worker/TodoProcessingWorker.cs`):

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    using var timer = new PeriodicTimer(_options.Interval, _timeProvider);
    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
        await ProcessOnceAsync(stoppingToken);
    }
}

public async Task ProcessOnceAsync(CancellationToken cancellationToken)
{
    await using var scope = _scopeFactory.CreateAsyncScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();

    try
    {
        var processed = await sender.Send(new ProcessPendingTodoItemsCommand(), cancellationToken);
        _logger.LogInformation("Processed {Count} pending todo item(s).", processed);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Tick failed; the loop continues on the next interval.");
    }
}
```

Two details here are requirements, not style, and matter if you extend the template:

- **One scope per tick, via `IServiceScopeFactory.CreateAsyncScope()`.**
  `AddHostedService<T>` registers `TodoProcessingWorker` as a singleton, while `ISender`,
  `ApplicationDbContext`, and `ITodoItemRepository` are all scoped. Resolving them from
  the worker's constructor (or from the root provider) would capture one `DbContext` for
  the entire process lifetime — an unbounded change tracker and stale reads after the
  first tick. Opening a fresh async scope per tick is what avoids it; the same pattern
  `Program.cs` already uses for the startup migration scope.
- **The per-tick `try`/`catch`.** .NET's default `BackgroundServiceExceptionBehavior` is
  `StopHost`: an unhandled exception in `ExecuteAsync` kills the whole process. Catching
  inside `ProcessOnceAsync` means one bad tick (a locked SQLite file, a transient error)
  logs and moves on instead of taking the worker down.

`ProcessPendingTodoItemsCommandHandler`
(`src/CleanArchWorkerService.Application/Todos/ProcessPendingTodoItems/`):

```csharp
public async Task<int> Handle(ProcessPendingTodoItemsCommand request, CancellationToken ct)
{
    var pending = await repository.GetPendingAsync(ct);
    foreach (var item in pending)
    {
        item.MarkComplete();
    }
    await repository.SaveChangesAsync(ct);
    return pending.Count;
}
```

"Pending" is `!IsComplete` — no new schema, no clock dependency in Domain, so the copied
EF Core migration and `ApplicationDbContextModelSnapshot` stay valid unchanged.
`TodoItem.MarkComplete()` is idempotent (a second call on an already-complete item is a
no-op and raises no event), and `ApplicationDbContext.SaveChangesAsync` already drains
`AggregateRoot.DomainEvents` through `IPublisher` after the commit, so no Infrastructure
change was needed for `TodoItemCompletedEvent` to fire from a worker tick.

## Configuration: `Worker:Interval`

```json
{ "Worker": { "Interval": "00:00:30" } }
```

`AddWorker(...)` (`src/CleanArchWorkerService.Worker/DependencyInjection/ServiceCollectionExtensions.cs`)
binds this section into `WorkerOptions` and validates it at startup:

```csharp
services
    .AddOptions<WorkerOptions>()
    .Bind(configuration.GetSection(WorkerOptions.SectionName))
    .Validate(o => o.Interval > TimeSpan.Zero, "Worker:Interval must be greater than zero.")
    .ValidateOnStart();
```

`PeriodicTimer` throws `ArgumentOutOfRangeException` on a non-positive period, deep
inside `ExecuteAsync` where it's hard to diagnose; `ValidateOnStart()` fails fast at
startup instead, with a message that names the setting. The interval is runtime
configuration, not a `.template.config` symbol — there is no `--interval` generation
flag, on purpose (see [ADR 0018](../adr/0018-worker-template-scoped-mvp.md)).

## Dev-only seed data

A worker has no inbound API, so an empty database means an invisible loop: nothing ever
appears to happen. `Program.cs` seeds two pending items on first run, but **only** when
`app.Environment.IsDevelopment()` and the table is empty:

```csharp
if (app.Environment.IsDevelopment() && !await dbContext.Items.AnyAsync())
{
    dbContext.Items.AddRange(TodoItem.Create("Write the design"), TodoItem.Create("Ship the worker"));
    await dbContext.SaveChangesAsync();
}
```

This has no equivalent in `webapi`/`grpc`, where the `CreateTodoItem`
RPC/endpoint supplies data interactively. Without it, `dotnet run --project
src/MyWorker.AppHost` on a fresh clone would show a healthy, ticking worker that never
logs `Processed N pending todo item(s).` with `N > 0` — indistinguishable from a worker
that's silently broken. The seed only ever runs once (guarded by `!AnyAsync()`), and
never runs outside `Development`, so it has no effect on a deployed instance.

## Testing strategy

Four tiers, mirroring `webapi`/`grpc`'s split (see
[`docs/adr/0012-four-tier-test-strategy.md`](../adr/0012-four-tier-test-strategy.md)).
Current counts in `templates/worker/tests/`:

| Project | Tests | Goal | Database |
|---|---|---|---|
| `Application.Tests` | 14 | Unit: handlers, validators, behaviors, domain entities (incl. `MarkComplete`, `ProcessPendingTodoItemsCommandHandler`, `TodoItemCompletedEventHandler`) | None |
| `Integration.Tests` | 5 | Real EF Core persistence via `Database.MigrateAsync()`, incl. `GetPendingAsync` and domain-event publication on save | SQLite (temp file) |
| `Architecture.Tests` | 6 | Fitness functions: Domain/Application/Infrastructure/Worker layering | N/A |
| `Functional.Tests` | 2 | Host integration via a real `IHost` (see below) | SQLite (temp file) |

`Functional.Tests` is the tier with no direct precedent: `webapi` and `grpc` both drive
their loop through an inbound request (`WebApplicationFactory<Program>` +
`HttpClient`/`GrpcChannel`), but a worker has no inbound protocol to call. Instead,
`WorkerHostFixture` boots a real `IHost` (`Host.CreateApplicationBuilder`) against a
temp SQLite database, with a `FakeTimeProvider`
(`Microsoft.Extensions.TimeProvider.Testing`) registered *before* `AddWorker(...)` runs
(`AddWorker` uses `TryAddSingleton`, so the fake wins). Two tests cover it:

1. **`ProcessOnceAsync_CompletesPendingItems_ThroughTheRealHost`** — seeds one pending
   item, resolves the running `TodoProcessingWorker` from the host's `IHostedService`
   collection, calls `ProcessOnceAsync` directly, then asserts `IsComplete` from a
   **fresh scope's** `DbContext`. Reading from a new scope is what would catch the
   captive-`DbContext` bug described above if it were ever reintroduced — a same-context
   assertion would pass even with a broken scope lifetime.
2. **`HostedService_ProcessesPendingItems_WhenTheTimerTicks`** — starts the host,
   advances the `FakeTimeProvider` by one interval, then polls
   (`WaitFor.UntilAsync`, a 5-second ceiling) until the item is marked complete. This is
   the end-to-end proof that the `PeriodicTimer` loop itself — not just
   `ProcessOnceAsync` in isolation — drives the work. The bounded poll exists because
   `FakeTimeProvider.Advance()` schedules the timer callback but the worker's
   continuation runs on the thread pool, so asserting immediately after `Advance()` can
   observe the pre-tick state; the fast path still completes in tens of milliseconds and
   the tier never waits on a real 30-second interval.

Repository-level proof that the template is self-contained and buildable outside this
repo lives in `templates/tests/WorkerTemplateGenerationTests.cs`, alongside
`webapi`'s and `grpc`'s equivalents (see [Adding a new
template](../contributing.md#adding-a-new-template)).

## Running the generated project

```bash
dotnet run --project src/MyWorker.AppHost
```

Aspire is not optional for `worker`: with no `--orchestrator` flag, the AppHost is
always generated and is the only supported way to run the service locally. It registers
the worker as a plain resource:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
builder.AddProject<Projects.MyWorker_Worker>("worker");
builder.Build().Run();
```

Open the Aspire dashboard link printed on startup; the `worker` resource should report
"Running" (thanks to `/health`/`/alive`, see [Why a background worker is hosted by
`WebApplication`](#why-a-background-worker-is-hosted-by-webapplication)), and its
console logs should show `Processed 2 pending todo item(s).` on the first tick after
startup, thanks to the [dev-only seed](#dev-only-seed-data).

## What's out of scope for this MVP

- **No `--database`/`--orm`/`--orchestrator`/`--trigger` flags.** SQLite + EF Core +
  Aspire + a `PeriodicTimer` is the only supported combination today. See
  [Scope](#scope-a-fixed-mvp-not-a-smaller-webapi) and ADR 0018.
- **No generated CI workflow.** `webapi`'s `.github/workflows/ci.yml` (see
  [Continuous Integration](./webapi.md#continuous-integration)) depends on a
  database-provider/orchestrator matrix `worker` doesn't have; deferred until
  provider/orchestrator parity lands, matching `grpc`'s posture.
- **One processing command, not a job scheduler.** `ProcessPendingTodoItems` proves a
  timer-driven write through the CQRS/domain-event pipeline; it isn't a cron-style
  multi-job scheduler or a message-queue consumer.

## Alternative: vanilla `dotnet new`, without the `dorn` CLI

Unlike `webapi` (see [the equivalent
section](./webapi.md#alternative-vanilla-dotnet-new-without-the-dorn-cli)),
`templates/worker` is **not** packed as a standalone NuGet template package
(`eng/scripts/pack-templates.ps1` only packs `Dorn.Templates.WebApi`), the same as
`grpc`. `dorn new worker` (the isolated `~/.dorn/template-engine` host) is the only way
to generate one right now; a `Dorn.Templates.Worker` package (ADR 0008) isn't
implemented yet.
