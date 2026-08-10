# 0007. Sync `templates/shared` → `templates/webapi` by Physical Copy + CI Check

## Status

Superseded by [ADR 0010](./0010-extract-messaging-and-shared-kernel-as-nuget-packages.md)

## Context

Early templates needed identical domain primitives and mediator code while remaining buildable outside the repository. Project references and symlinks would break that self-contained output.

## Decision

Keep canonical files under `templates/shared/`, copy them physically into `templates/webapi/`, and fail CI when byte-for-byte comparison detects drift.

## Consequences

- Generated projects stayed self-contained.
- Drift became visible in CI.
- Every shared edit still required manual copies.
- Adding templates multiplied the sync surface.

## Superseding decision

ADR 0010 moved shared code into versioned NuGet packages and removed `templates/shared/` plus the sync script. The self-containment requirement remains active.

## Alternatives

- **Project references outside the template:** rejected because generated projects leave the repository.
- **Symlinks:** rejected for archive and cross-platform reliability.

## Related

- [ADR 0003: Custom mediator](./0003-custom-mediator-instead-of-mediatr.md)
- [ADR 0010: Shared packages](./0010-extract-messaging-and-shared-kernel-as-nuget-packages.md)
