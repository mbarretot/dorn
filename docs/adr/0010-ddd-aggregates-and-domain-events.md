# 0010. DDD Aggregates and Domain Events

## Status

Accepted

## Context

`webapi`'s domain layer had a single `BaseEntity` type providing both identity (`Id`) and
a domain-event collection (`AddDomainEvent`/`RemoveDomainEvent`/`ClearDomainEvents`,
typed `IReadOnlyCollection<object>`). Every entity — not just aggregate roots — inherited
the ability to raise events, which doesn't match DDD terminology: in DDD, only an
*aggregate root* is a consistency boundary that publishes what happened inside it. A plain
entity that's part of an aggregate (owned by, and only reachable through, its root) has no
business raising events of its own. `BaseEntity.AddDomainEvent` was also `public`, which
meant any code holding a reference to an entity — not just the entity's own methods —
could push an event onto it, letting external code claim something happened to an
aggregate that the aggregate itself never decided. `RemoveDomainEvent` existed but a repo
grep found it unused everywhere (outside `ejemplo/`, an unrelated local scratch directory
excluded from this search).

Separately, the mediator (ADR 0003) only supported request/response dispatch
(`IRequest`/`IRequestHandler`/`ISender`) — there was no publish/subscribe mechanism for an
aggregate to announce a domain event to zero-or-more interested handlers after it's
persisted. ADR 0003 explicitly called this out as an intentional gap ("no built-in
notification/publish (`INotification`) support"), deferred until a template actually
needed it. `webapi`'s `TodoItem` creation flow is the first concrete case: something should
observe "a todo item was created" without `CreateTodoItemCommandHandler` taking on that
responsibility directly.

## Decision

**Split `BaseEntity` into `Entity` and `AggregateRoot : Entity`.** `Entity` keeps identity
and equality (`Id`, `Equals`/`GetHashCode`/`==`/`!=` by `Id` + runtime type). `AggregateRoot`
adds the domain-event collection. Only aggregate roots raise events —
`TodoItem : AggregateRoot`, not `TodoItem : Entity`. `AddDomainEvent` is now `protected`
(only the aggregate's own methods may call it — `TodoItem.Create` calls it internally, no
external caller can), `ClearDomainEvents` stays `public` (the infrastructure layer needs to
clear events after dispatch, from outside the aggregate). `RemoveDomainEvent` is dropped —
confirmed dead API surface, no reason to carry it forward.

**`INotification` lives in `Domain`, not `Application/Messaging`.** This is the single most
important design decision here, because it's easy to get backwards: `AggregateRoot` needs
`DomainEvents` typed `IReadOnlyCollection<INotification>` (not `IReadOnlyCollection<object>`
— an untyped collection defeats the purpose of having a marker interface at all), and
`AggregateRoot` lives in `Domain`. If `INotification` lived in `Application.Messaging`
instead, `Domain` would have to reference `Application` to implement it, inverting Clean
Architecture's dependency rule: `Domain` must have zero dependencies, and `Application`
depends on `Domain`, never the other way around. Putting `INotification` in `Domain`
instead means `Application.Messaging`'s `INotificationHandler<TNotification>` and
`IPublisher` reference `INotification` from `Domain` — which is the correct, already-
established direction (`Application` already depends on `Domain` for `IRequest`,
`TodoItem`, etc.).

**Dispatch happens in `ApplicationDbContext.SaveChangesAsync`, after the save succeeds, not
before.** The override captures every tracked `AggregateRoot` with pending events *before*
calling `base.SaveChangesAsync`, calls the base save, and only then — if it didn't throw —
clears each aggregate's events and publishes them one by one via `IPublisher`. Publishing
before the save, or from inside a `SaveChanges`-triggered interceptor that runs regardless
of outcome, would let a `TodoItemCreatedEventHandler` (or any other handler) observe a
"created" event for a row that never actually made it into the database if the save later
fails. Capturing events before the save (rather than after) matters because
`ChangeTracker.Entries<AggregateRoot>()` still reflects `Added`/`Modified` state at that
point — after a successful save EF flips tracked entities to `Unchanged`, but the
`DomainEvents` collection itself is unaffected by that state change, so ordering here is
about correctness of intent (dispatch only what was about to be persisted), not about a
collection that would otherwise go empty.

**Handlers are plain `INotificationHandler<T>`, auto-registered by the existing
`AddMediator` scan** — no new registration mechanism. `ServiceCollectionExtensions.AddMediator`
already scanned an assembly's concrete types for `IRequestHandler<,>` and
`IPipelineBehavior<,>` implementations and called `services.AddTransient(implementedInterface,
type)` for each; adding `INotificationHandler<>` to that same condition was enough, because
`AddTransient` already supports multiple registrations of the same interface (needed here,
since more than one handler can subscribe to the same notification type — that's the whole
point of publish/subscribe over point-to-point request/response). `Mediator.Publish`
resolves every registered `INotificationHandler<>` for the notification's runtime type via
`IServiceProvider.GetServices` (plural) and invokes each in turn, mirroring the reflection
pattern `Send` already used for the single-handler request case.

**Worked example**: `TodoItem.Create(string title)` — a static factory replacing the old
public-settable object initializer — constructs the entity and calls
`AddDomainEvent(new TodoItemCreatedEvent(todoItem.Id, todoItem.Title))` before returning it.
`TodoItemCreatedEventHandler : INotificationHandler<TodoItemCreatedEvent>` logs the event via
`ILogger<TodoItemCreatedEventHandler>` (resolved for free — ASP.NET Core's
`WebApplication.CreateBuilder` wires up logging by default, no `Program.cs` change needed).
`CreateTodoItemCommandHandler` calls `TodoItem.Create(request.Title)` instead of `new
TodoItem { Title = ... }`, and `ApplicationDbContext.SaveChangesAsync` does the rest.

## Consequences

- Domain-event ownership is now enforced by the compiler, not by convention: an aggregate's
  own methods are the only code that can call `AddDomainEvent`, closing the encapsulation
  gap the old public `BaseEntity.AddDomainEvent` had.
- `Entity`/`AggregateRoot`/`INotification` (three files replacing `BaseEntity.cs`) stayed
  physically synced between the canonical shared-source directory and
  `templates/webapi/src/CleanArchWebApi.Domain/` per ADR 0008;
  `INotificationHandler.cs`/`IPublisher.cs` (two new files) joined the nine-file
  messaging sync set. (Update, ADR 0011: this physical-copy mechanism was later retired —
  `Entity`/`AggregateRoot`/`INotification`/the mediator now ship as the
  `Dorn.SharedKernel`/`Dorn.Messaging.Contracts`/`Dorn.Messaging` NuGet packages under
  `packages/`, consumed via `PackageReference`. The type split and dependency direction
  described in this ADR are unchanged, only the sharing mechanism moved.)
- This is intentionally minimal, matching the scaffold philosophy of ADR 0003: no
  notification pipeline behaviors (no `IPipelineBehavior<,>`-equivalent for notifications),
  no async/fire-and-forget dispatch strategy, and no outbox pattern. Dispatch is sequential
  and in-process, inside the same `SaveChangesAsync` call that persisted the triggering
  change — which means a throwing `INotificationHandler<T>` currently fails the whole
  save-and-publish flow, propagating the exception back to the caller of `SaveChangesAsync`.
  This is an accepted trade-off for a scaffold's default, not a production-hardened event
  bus; a project that needs resilient delivery (retries, at-least-once guarantees, ordering
  across process restarts) should replace this with an outbox table and a separate
  dispatcher, same as it would need to replace SQLite for a multi-instance deployment (ADR
  0005).
- If a future template needs the same domain-event pattern, `Entity`/`AggregateRoot`/
  `INotification` and the notification half of the mediator are already isolated in
  `packages/Dorn.SharedKernel/`/`packages/Dorn.Messaging.Contracts/`, ready to be consumed
  via `PackageReference` the same way `webapi` itself does (ADR 0011).
