# 0010. Extract Messaging and Shared Kernel as NuGet Packages

## Status

Accepted

## Context

ADR 0007 copied shared source into each template and used CI to detect drift. That approach required manual synchronization and scaled poorly beyond one template.

## Decision

Create three packages under top-level `packages/`:

| Package | Contents | Depends on |
| --- | --- | --- |
| `Dorn.Messaging.Contracts` | Requests, handlers, behaviors, notifications, sender, publisher | BCL only |
| `Dorn.Messaging` | Mediator and DI registration | Messaging.Contracts |
| `Dorn.SharedKernel` | `Entity`, `AggregateRoot`, `Result` | Messaging.Contracts |

`INotification` belongs to Messaging.Contracts because it is the wire contract between aggregate events and notification handlers. Templates consume these packages through their own centrally managed `PackageReference` entries.

## Consequences

- Shared code has one canonical implementation.
- New templates add package references instead of copy pairs.
- Generated projects stay self-contained and restore normal versioned packages.
- Local package changes still require `eng/scripts/pack-packages.ps1` and the `./artifacts` feed during repository tests.
- Package versioning becomes part of template compatibility.

## Alternatives

- **Continue physical sync:** rejected due to manual work and template-count growth.
- **Place packages under `src/`:** rejected because `src/` owns the CLI runtime, while `packages/` ship into generated applications.

## Related

- [ADR 0007: Superseded physical sync](./0007-templates-shared-physical-copy-sync.md)
- [Architecture](../architecture.md)
