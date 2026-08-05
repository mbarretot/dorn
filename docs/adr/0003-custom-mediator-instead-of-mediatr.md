# 0003. Custom Mediator Instead of MediatR

## Status

Accepted

## Context

The `webapi` template uses a CQRS-style Application layer: HTTP endpoints send a request
object (`IRequest<TResponse>`) through an indirection (`ISender`) that dispatches to the
matching handler, optionally through pipeline behaviors first. MediatR is the de facto
standard for this pattern, but as of MediatR v13 (July 2025) it moved from Apache 2.0 to
RPL-1.5, a source-available, non-OSI license requiring either a paid commercial license
or specific usage conditions. Every project Dorn generates would inherit that licensing,
undermining Dorn's own goal of MIT-license, maximum-permissiveness community adoption.

## Decision

> **Update (ADR 0010):** this mediator now ships as the `Dorn.Messaging.Contracts` and
> `Dorn.Messaging` NuGet packages under `packages/`, consumed via `PackageReference`,
> rather than as physically-copied source. The decision below (a from-scratch,
> MIT-licensed mediator instead of MediatR) is unchanged; only the distribution mechanism
> moved. See `docs/adr/0010-extract-messaging-and-shared-kernel-as-nuget-packages.md`.

The mediator was implemented from scratch as MIT-licensed, MediatR-shaped source code:

- `IRequest<TResponse>` / `IRequest` (the latter is `IRequest<Unit>`, with `Unit` a
  zero-information struct).
- `IRequestHandler<TRequest, TResponse>` with a single
  `Handle(TRequest, CancellationToken)` method.
- `ISender.Send<TResponse>(IRequest<TResponse>, CancellationToken)`.
- `IPipelineBehavior<TRequest, TResponse>.Handle(TRequest, RequestHandlerDelegate<TResponse>, CancellationToken)`.

`Mediator : ISender` resolves the handler for a request's runtime type via
`IServiceProvider` (reflection over the closed `IRequestHandler<,>` generic) and wraps
the call in every registered `IPipelineBehavior<,>`, the same decorator-chain mechanism
MediatR uses internally, implemented directly instead of depended on.
`ServiceCollectionExtensions.AddMediator(this IServiceCollection, Assembly)` scans an
assembly for `IRequestHandler<,>`/`IPipelineBehavior<,>` implementations and registers
them, alongside `ISender → Mediator`.

This code now lives in `packages/Dorn.Messaging.Contracts/` and `packages/Dorn.Messaging/`,
consumed by `templates/webapi` via `PackageReference` (ADR 0010) so it stays identical
across every template adopting the same CQRS pattern.

## Consequences

- Every generated project has zero external dependencies for its CQRS infrastructure,
  unambiguously MIT-licensed source it owns outright: no RPL-1.5 question ever arises.
- **Intentionally minimal** compared to MediatR: no assembly-scanning options beyond a
  single `Assembly` parameter, no exception-handling middleware hooks. (Notification
  support, `INotification`/`IPublisher`, was added later, see ADR 0009.) Contributors
  extending this add features directly to the packages rather than getting them free
  from upstream.
- As of ADR 0010, updates are ordinary package version bumps
  (`eng/scripts/pack-packages.ps1`) rather than a copy-and-diff-check step.
