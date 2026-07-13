# 0003. Custom Mediator Instead of MediatR

## Status

Accepted

## Context

The `webapi` template uses a CQRS-style Application layer: HTTP endpoints don't call
handler classes directly, they send a request object (`IRequest<TResponse>`) through an
indirection (`ISender`) that dispatches to the matching handler, optionally passing
through cross-cutting pipeline behaviors (validation, logging, transactions) first.
MediatR is the de facto standard library for this pattern in the .NET ecosystem and was
the obvious default choice.

However, as of MediatR v13 (July 2025), the project changed its license from the
permissive Apache 2.0 to RPL-1.5 — a source-available but not OSI-approved license that
requires either a paid commercial license or specific conditions for use, depending on
how the project is distributed and used. Every project Dorn generates would inherit
whatever licensing terms its `webapi` template depends on. Given Dorn's own goal (ADR
0007: MIT license, maximum permissiveness for community adoption), taking a dependency
that could require generated-project authors to pay for a commercial license, or to
carefully evaluate RPL-1.5 compliance, directly undermines that goal.

## Decision

> **Update (ADR 0011):** this mediator now ships as the `Dorn.Messaging.Contracts` and
> `Dorn.Messaging` NuGet packages under `packages/`, consumed via `PackageReference`,
> rather than as physically-copied source. The decision below — a from-scratch,
> MIT-licensed mediator instead of MediatR — is unchanged; only the distribution
> mechanism moved. See
> `docs/adr/0011-extract-messaging-and-shared-kernel-as-nuget-packages.md`.

The mediator was implemented from scratch as MIT-licensed, MediatR-shaped source code
(not a published NuGet package in v1 — see the "Decisiones tomadas por defecto" note on
distribution in the original planning document):

- `IRequest<TResponse>` / `IRequest` (the latter is `IRequest<Unit>`, with `Unit` a
  zero-information struct for "no meaningful return value").
- `IRequestHandler<TRequest, TResponse>` with a single `Handle(TRequest, CancellationToken)` method.
- `ISender.Send<TResponse>(IRequest<TResponse>, CancellationToken)`.
- `IPipelineBehavior<TRequest, TResponse>.Handle(TRequest, RequestHandlerDelegate<TResponse>, CancellationToken)`.

`Mediator : ISender` resolves the handler for a request's runtime type via
`IServiceProvider` (reflection over the closed `IRequestHandler<,>` generic), then wraps
the call in every registered `IPipelineBehavior<,>` for that request/response pair —
same decorator-chain mechanism MediatR itself uses internally, just implemented directly
rather than depended on. `ServiceCollectionExtensions.AddMediator(this IServiceCollection,
Assembly)` scans an assembly's concrete classes for `IRequestHandler<,>` and
`IPipelineBehavior<,>` implementations and registers them, alongside `ISender → Mediator`.

This code now lives in `packages/Dorn.Messaging.Contracts/` and `packages/Dorn.Messaging/`,
consumed by `templates/webapi` via `PackageReference` (see ADR 0011) so it stays identical
across every template that adopts the same CQRS pattern.

## Consequences

- Every project Dorn generates has zero external dependencies for its CQRS
  infrastructure, and that infrastructure is unambiguously MIT-licensed source the
  project owns outright — no commercial license or RPL-1.5 compliance question ever
  arises.
- The implementation is intentionally minimal compared to MediatR: no assembly-scanning
  configuration options beyond a single `Assembly` parameter, no exception-handling
  middleware hooks. (Notification/publish support — `INotification`, `IPublisher` — was
  added later, see ADR 0010.) Contributors extending this need to add such features
  directly to `packages/Dorn.Messaging.Contracts/`/`packages/Dorn.Messaging/` rather than
  getting them for free from an upstream package.
- As of ADR 0011, updates to the mediator are ordinary package version bumps
  (`packages/Dorn.Messaging.Contracts/`, `packages/Dorn.Messaging/`, repacked via
  `eng/scripts/pack-packages.ps1`) rather than a copy-and-diff-check step.
- If MediatR's licensing changes again in the future (or Dorn decides the tradeoff is
  worth it for a richer feature set), this decision can be revisited without touching
  `Dorn.Abstractions`/`Dorn.Core` — the mediator lives entirely inside the templates.
