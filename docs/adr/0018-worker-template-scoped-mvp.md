# 0018. Worker Template as a Scoped MVP

## Status

Accepted

## Context

Web API and gRPC templates are request-driven. A worker proves the same inner layers can run from a non-transport trigger without adding a broker or scheduler product.

## Decision

Ship `dorn-worker` with one fixed profile:

| Concern | Choice |
| --- | --- |
| Persistence | EF Core + SQLite |
| Orchestration | Aspire |
| Trigger | `PeriodicTimer` |
| Work | Complete pending Todo items and publish domain events |
| Interval | Runtime `Worker:Interval`, default `00:00:30` |

Each tick creates an async DI scope before resolving `ISender`. The tick catches operational failures so one transient error does not stop the host, while cancellation still propagates.

The host uses `WebApplication` only to expose Aspire `/health` and `/alive`. It has no business HTTP API.

Functional tests run a real host with `FakeTimeProvider` and verify persistence through a fresh scope.

## Consequences

- `dorn new worker MyWorker` runs through AppHost with no choices.
- Development-only seed data makes the first tick observable.
- Scoped persistence cannot leak into the singleton background service.
- Other triggers, providers, orchestrators, CI scaffolding, and standalone template packaging are deferred.

## Alternatives

- **Message broker trigger:** rejected because it adds infrastructure and vendor choices before the worker pattern is proven.
- **Constructor-inject scoped services:** rejected as a captive dependency.
- **Generation-time interval:** rejected because interval is runtime configuration.

## Related

- [Worker template](../templates/worker.md)
- [ADR 0015: gRPC scoped MVP](./0015-grpc-template-scoped-mvp.md)
