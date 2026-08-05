# 0009. DDD Aggregates and Domain Events

## Status

Accepted

## Context

`webapi`'s domain layer had a single `BaseEntity` type providing both identity (`Id`) and
a domain-event collection (`AddDomainEvent`/`RemoveDomainEvent`/`ClearDomainEvents`, typed
`IReadOnlyCollection<object>`). Three problems: every entity, not just aggregate roots,
could raise events, though in DDD only an aggregate root is a consistency boundary that
publishes what happened inside it; `AddDomainEvent` was `public`, so any code holding a
reference could push an event onto an entity; and `RemoveDomainEvent` was unused
everywhere.

Separately, the mediator (ADR 0003) only supported request/response dispatch; ADR 0003
explicitly called out the missing publish/subscribe mechanism as an intentional gap,
deferred until a template needed it. `webapi`'s `TodoItem` creation flow is the first
concrete case: something should observe "a todo item was created" without
`CreateTodoItemCommandHandler` taking on that responsibility directly.

## Decision

- **Split `BaseEntity` into `Entity` and `AggregateRoot : Entity`.** `Entity` keeps
  identity and equality (`Id`, `Equals`/`GetHashCode`/`==`/`!=` by `Id` + runtime type).
  `AggregateRoot` adds the domain-event collection; only aggregate roots raise events
  (`TodoItem : AggregateRoot`). `AddDomainEvent` is now `protected` (only the aggregate's
  own methods may call it); `ClearDomainEvents` stays `public` (infrastructure clears
  events after dispatch). `RemoveDomainEvent` is dropped as confirmed dead code.

- **`INotification` lives in `Domain`, not `Application/Messaging`.** `AggregateRoot`
  needs `DomainEvents` typed `IReadOnlyCollection<INotification>`, and `AggregateRoot`
  lives in `Domain`. If `INotification` lived in `Application.Messaging` instead,
  `Domain` would have to reference `Application` to implement it, inverting Clean
  Architecture's dependency rule (`Domain` has zero dependencies). Keeping it in `Domain`
  lets `Application.Messaging`'s `INotificationHandler<TNotification>`/`IPublisher`
  reference it the same direction `Application` already depends on `Domain`.

- **Dispatch happens in `ApplicationDbContext.SaveChangesAsync`, after the save
  succeeds, not before.** The override captures every tracked `AggregateRoot` with
  pending events before calling `base.SaveChangesAsync`, then, only if it didn't throw,
  clears each aggregate's events and publishes them via `IPublisher`. Publishing before
  the save (or from an outcome-independent interceptor) would let a handler observe a
  "created" event for a row that never made it into the database.

- **Handlers are plain `INotificationHandler<T>`, auto-registered by the existing
  `AddMediator` scan.** Adding `INotificationHandler<>` to the same scan condition that
  already registered `IRequestHandler<,>`/`IPipelineBehavior<,>` was enough;
  `AddTransient` already supports multiple registrations of the same interface, which
  publish/subscribe needs. `Mediator.Publish` resolves every registered handler for the
  notification's runtime type via `IServiceProvider.GetServices` and invokes each in
  turn.

- **Worked example.** `TodoItem.Create(string title)`, a static factory replacing the old
  public-settable object initializer, calls `AddDomainEvent(new
  TodoItemCreatedEvent(todoItem.Id, todoItem.Title))` before returning.
  `TodoItemCreatedEventHandler : INotificationHandler<TodoItemCreatedEvent>` logs the
  event via `ILogger<TodoItemCreatedEventHandler>`. `CreateTodoItemCommandHandler` calls
  `TodoItem.Create(request.Title)` instead of `new TodoItem { Title = ... }`.

## Consequences

- Domain-event ownership is now enforced by the compiler: only an aggregate's own
  methods can call `AddDomainEvent`.
- `Entity`/`AggregateRoot`/`INotification` (three files replacing `BaseEntity.cs`) stayed
  physically synced between `templates/shared/` and `templates/webapi/` per ADR 0007;
  `INotificationHandler.cs`/`IPublisher.cs` joined the nine-file messaging sync set.
  *Update, ADR 0010:* this physical-copy mechanism was later retired in favor of the
  `Dorn.SharedKernel`/`Dorn.Messaging.Contracts`/`Dorn.Messaging` NuGet packages; the type
  split and dependency direction are unchanged, only the sharing mechanism moved.
- Intentionally minimal, matching ADR 0003's scaffold philosophy: no notification
  pipeline behaviors, no async/fire-and-forget dispatch, no outbox pattern. Dispatch is
  sequential and in-process inside the same `SaveChangesAsync` call; a throwing
  `INotificationHandler<T>` currently fails the whole save-and-publish flow. Accepted as
  a scaffold default; a project needing resilient delivery should replace this with an
  outbox table and separate dispatcher, the same way it would replace SQLite for a
  multi-instance deployment (ADR 0005).
- If a future template needs the same domain-event pattern, `Entity`/`AggregateRoot`/
  `INotification` and the notification half of the mediator are already isolated in
  `packages/Dorn.SharedKernel/`/`packages/Dorn.Messaging.Contracts/`, ready to be consumed
  via `PackageReference` the same way `webapi` does (ADR 0010).
