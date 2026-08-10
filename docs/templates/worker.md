# Worker Template

Generate a timer-driven Clean Architecture service with SQLite, EF Core, Aspire, and a full CQRS write path.

## ⚡ Quick path

```bash
dorn new worker MyWorker
cd MyWorker
dotnet dorn test
dotnet dorn run
```

## 🎯 Fixed profile

| Concern | Choice |
| --- | --- |
| Database | SQLite |
| ORM | EF Core |
| Orchestrator | Aspire |
| Trigger | `PeriodicTimer` |
| Tests | Included by default |

The command accepts `<name>`, `-o|--output`, `--force`, and `--no-restore`. Database, ORM, orchestrator, trigger, and interval are intentionally not generation choices.

## 🏛️ Generated shape

| Project | Responsibility |
| --- | --- |
| `<Name>.Domain` | Entities, aggregates, and domain events |
| `<Name>.Application` | CQRS requests, handlers, validation, and ports |
| `<Name>.Infrastructure` | EF Core, SQLite migrations, and repositories |
| `<Name>.Worker` | Background loop and runtime configuration |
| `<Name>.AppHost` | Aspire orchestration |
| `<Name>.ServiceDefaults` | Telemetry, health, and resilience defaults |

## ⏱️ Processing flow

```text
PeriodicTimer
  -> TodoProcessingWorker
    -> new DI scope
      -> ISender
        -> ProcessPendingTodoItemsCommand
          -> complete pending items
            -> save
              -> publish domain events
```

Two rules keep the loop safe:

1. **One async scope per tick.** The singleton `BackgroundService` never captures scoped `ISender`, repository, or `DbContext` instances.
2. **One exception boundary per tick.** A transient failure is logged and the next interval still runs. Cancellation continues to stop the host.

## ⚙️ Runtime configuration

```json
{
  "Worker": {
    "Interval": "00:00:30"
  }
}
```

`Worker:Interval` must be greater than zero and is validated at startup. It is runtime configuration, not a template flag.

Development starts with two pending items only when the table is empty. This makes the first timer tick visible without affecting other environments.

## ❤️ Why the worker uses `WebApplication`

The worker has no business API, but Aspire needs `/health` and `/alive` to report resource health. `WebApplication` hosts only those endpoints; no Todo HTTP or gRPC surface is exposed.

## 🧪 Test tiers

| Tier | Verifies |
| --- | --- |
| Application | Handlers, validators, behaviors, and domain logic |
| Integration | EF Core against a temporary SQLite file |
| Architecture | Layer dependency rules |
| Functional | Real host, fake time, timer tick, and persistence from a fresh scope |

The functional tier uses `FakeTimeProvider`, so it never waits for the real 30-second interval.

## 🚧 Intentional limits

- No database, ORM, orchestrator, or trigger choices.
- No generated CI workflow.
- No standalone `Dorn.Templates.Worker` NuGet template package.
- One processing command, not a scheduler or message broker consumer.

Generate through `dorn new worker`; vanilla `dotnet new install` is not available for this template.

## 📚 Related

- [Scoped MVP decision](../adr/0018-worker-template-scoped-mvp.md)
- [Architecture](../architecture.md)
- [Web API CQRS details](./webapi.md#-cqrs-and-domain-events)
