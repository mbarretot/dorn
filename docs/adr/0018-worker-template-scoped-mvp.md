# 0018. Worker Template as a Scoped MVP

## Status

Accepted

## Context

`webapi` (configurable) and `grpc` (fixed MVP, ADR 0015) are both request-driven:
something outside the process calls in and gets a response. Nothing in the catalog
covers a background or scheduled process, the third common .NET service shape. A worker
is also the cheapest remaining proof that Dorn's Domain/Application layers are
presentation-agnostic: `grpc` proved it for a second *transport*, `worker` proves it for
a *non-transport* trigger — nothing calls in at all. Exploration confirmed
`TemplateLocator`/`FileSystemTemplateCatalog`/`Scanner` discover any
`templates/<name>/.template.config` with zero change to `Dorn.Core`.

## Decision

Ship `templates/worker/` (short name `dorn-worker`, identity `Dorn.Templates.Worker`) as
a **fixed-scope MVP**, the same posture ADR 0015 chose for `grpc`: SQLite + EF Core +
Aspire only, no `--database`, `--orm`, `--orchestrator`, or `--trigger` flag.
`NewWorkerCommand`/`NewWorkerSettings` (`src/Dorn.Cli/Commands/New/`) accept only
`<name>`, `-o|--output`, `--force`, and `--no-restore`.

1. **Timer, not queue.** A `PeriodicTimer`-driven `BackgroundService` is the trigger. A
   message-consumer trigger would add a broker dependency and an Infrastructure concern
   with no precedent in this codebase — exactly the multiplication ADR 0015 rejected for
   `grpc`'s presentation surface.
2. **Aspire is not optional**, mirroring ADR 0015. `<Name>.AppHost`/`<Name>.ServiceDefaults`
   always generate, so `dorn run`'s `RunAspire()` path always applies and
   `ProjectContextResolver`'s `.WebApi`-suffix-only Plain resolution is never reached.
   `--orchestrator none` for workers is a deliberate follow-up ADR with the
   `ProjectContextResolver`/`RunCommand` fix as its named prerequisite, exactly as
   ADR 0015 deferred SQL Server/PostgreSQL/Dapper for `grpc`.
3. **`WebApplication.CreateBuilder` despite having no API.** `AddServiceDefaults()` and
   `MapDefaultEndpoints()` are `IHostApplicationBuilder`/`WebApplication` extensions, and
   `MapDefaultEndpoints()` is the Aspire dashboard's health probe; without them the
   dashboard never reports the worker healthy and the resource shows perpetually
   "Starting". `/health` and `/alive` are the only mapped endpoints — there is no
   `MapGet`, no OpenAPI, no `MapGrpcService`. Unlike `grpc`, there is no
   `Kestrel:EndpointDefaults:Protocols` override: that entry existed solely for gRPC's
   HTTP/2 ALPN negotiation and has no analogue here.
4. **One scope per tick.** A `BackgroundService` is registered as a singleton by
   `AddHostedService<T>`, while `ISender`, `ApplicationDbContext`, and
   `ITodoItemRepository` are all scoped. Constructor-injecting `ISender` into the worker
   would be a captive dependency: one `DbContext` for the process lifetime, an unbounded
   change tracker, and stale reads after the first tick. `TodoProcessingWorker` instead
   resolves `IServiceScopeFactory` and calls `CreateAsyncScope()` once per tick,
   mirroring the migration scope `Program.cs` already opens. Each tick also catches its
   own exceptions: .NET's default `BackgroundServiceExceptionBehavior.StopHost` would
   otherwise let one transient failure (a locked SQLite file, for example) terminate the
   whole host.
5. **The example is a write, not a read.** `ProcessPendingTodoItemsCommand` completes
   every `!IsComplete` item and raises `TodoItemCompletedEvent`, which rides the existing
   `ApplicationDbContext.SaveChangesAsync` publisher — zero Infrastructure change was
   needed for events to fire from a worker tick. This exercises the full write path and
   domain-event pipeline from a non-transport trigger; a read-only query would have
   proven only that DI resolves. It costs one domain method (`TodoItem.MarkComplete()`),
   one event, one repository member (`GetPendingAsync`), and one handler; everything else
   copies from `grpc` unchanged.
6. **The interval is configuration, not a generation flag.** `Worker:Interval` defaults
   to `00:00:30` in `appsettings.json`, validated `> TimeSpan.Zero` via
   `.Validate(...).ValidateOnStart()`. Adding a `--interval` symbol would bake a runtime
   knob into generation time, which is not what `.template.config` symbols are for.
7. **The Functional tier is host integration, not a round-trip.** A worker has no
   inbound protocol, so `WebApplicationFactory<Program>` and `grpc`'s
   `ResponseVersionHandler` have no analogue. `WorkerHostFixture` boots a real `IHost`
   against a temp SQLite database with a `FakeTimeProvider` registered ahead of
   `AddWorker` (so `TryAddSingleton` yields to it), and asserts the mutation from a
   *fresh* scope — the scope that proves the D3-style captive-dependency bug would
   actually be caught. Worker registrations live in an `AddWorker(...)` extension
   (`<Name>.Worker/DependencyInjection/ServiceCollectionExtensions.cs`) precisely so the
   test fixture composes the same code path `Program.cs` does, instead of duplicating it
   and drifting.
8. **Delivered as a feature-branch PR chain**, each slice under the 400-line budget,
   mirroring ADR 0014's and ADR 0015's stacked/chained-PR pattern: template foundation +
   Domain, Application, Infrastructure, `<Name>.Worker` host, Aspire AppHost +
   ServiceDefaults, Application.Tests + Integration.Tests, Architecture.Tests +
   Functional.Tests, the CLI command + `Dorn.slnx` wiring + generation test, and this
   documentation slice.

## Consequences

- `dorn new worker MyWorker` needs no flags and runs via
  `dotnet run --project src/MyWorker.AppHost`; a Development-only seed
  (`Program.cs`, gated on `app.Environment.IsDevelopment()`) inserts two pending items on
  first run so the loop visibly does real work against an otherwise-empty database.
- Adding `--orchestrator none` later requires the `ProjectContextResolver`/`RunCommand`
  Worker-suffix resolution named above — bounded and well-precedented, not a design
  change.
- No generated `.github/workflows/ci.yml` (ADR 0013 is `webapi`-only) and no standalone
  `dotnet new` template pack (ADR 0008; `eng/scripts/pack-templates.ps1` packs `webapi`
  only) — `worker` matches `grpc`'s posture on both counts.
- The processing surface is intentionally one command; `webapi`/`grpc`'s
  `CreateTodoItem`/`GetTodoItems` copy over unchanged so the Application layer stays
  comparable across all three templates.
- ADR 0011/0014 (`webapi` provider choices) and ADR 0015 (`grpc`) are unaffected: none is
  superseded by this ADR.
