# 0009. DDD Aggregates and Domain Events

## Status

Accepted

## Context

The original `BaseEntity` let every entity raise loosely typed events from public code. The mediator also lacked publish and subscribe support.

## Decision

- Split identity into `Entity` and event ownership into `AggregateRoot : Entity`.
- Make `AddDomainEvent` protected; Infrastructure may call `ClearDomainEvents` after dispatch.
- Type events as `INotification` and handlers as `INotificationHandler<T>`.
- Publish events from `ApplicationDbContext.SaveChangesAsync` only after the database save succeeds.
- Resolve and invoke every notification handler sequentially.
- Use `TodoItem.Create` and `TodoItemCreatedEvent` as the worked example.

ADR 0010 later placed `INotification` in `Dorn.Messaging.Contracts` so both `Dorn.SharedKernel` and mediator handlers depend on the same contract.

## Consequences

- Only aggregate methods can raise their own events.
- A failed persistence operation publishes nothing.
- A failing handler still fails the save-and-publish call after persistence has committed.
- Dispatch is synchronous and in-process, with no retry or durable delivery.

## Alternatives

- **Events before persistence:** rejected because handlers could observe data that never committed.
- **Fire-and-forget dispatch:** rejected because failures would be hidden.
- **Outbox:** deferred for projects that require durable, resilient delivery.

## Related

- [ADR 0003: Custom mediator](./0003-custom-mediator-instead-of-mediatr.md)
- [ADR 0010: Shared packages](./0010-extract-messaging-and-shared-kernel-as-nuget-packages.md)
