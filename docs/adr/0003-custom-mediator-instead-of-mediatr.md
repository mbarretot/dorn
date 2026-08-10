# 0003. Custom Mediator Instead of MediatR

## Status

Accepted

## Context

Dorn templates need request dispatch and pipeline behaviors. MediatR v13 moved to RPL-1.5, which does not match Dorn's MIT-first distribution goal for generated projects.

## Decision

Maintain a small, MIT-licensed mediator with a MediatR-shaped API:

- `IRequest<TResponse>` and `IRequestHandler<TRequest, TResponse>`
- `ISender.Send`
- `IPipelineBehavior<TRequest, TResponse>`
- `INotification`, `INotificationHandler<T>`, and `IPublisher.Publish`
- `AddMediator(Assembly)` for handler and behavior discovery

The implementation ships through `Dorn.Messaging.Contracts` and `Dorn.Messaging`. ADR 0010 replaced the original copied-source distribution, not this mediator decision.

## Consequences

- Generated projects avoid MediatR licensing conditions.
- Dorn owns compatibility, maintenance, and feature growth.
- The API is intentionally smaller than MediatR.
- Notifications run sequentially and in-process.

## Alternatives

- **MediatR:** rejected for license fit.
- **Direct handler calls:** rejected because cross-cutting behaviors and transport-independent dispatch are core template patterns.

## Related

- [ADR 0009: Domain events](./0009-ddd-aggregates-and-domain-events.md)
- [ADR 0010: NuGet packages](./0010-extract-messaging-and-shared-kernel-as-nuget-packages.md)
