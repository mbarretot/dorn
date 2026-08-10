# 0006. xUnit + NSubstitute Over FluentAssertions/Moq

## Status

Accepted

## Context

Generated tests need a stable framework, assertion style, and mocking library. FluentAssertions moved to a commercial license in v8, while Moq's SponsorLink incident created a trust concern.

## Decision

Use:

| Need | Choice |
| --- | --- |
| Test framework | xUnit |
| Assertions | Plain `Assert.*` |
| Test doubles | NSubstitute |

This applies to repository tests and generated template tests.

## Consequences

- Test dependencies fit Dorn's permissive licensing goal.
- Contributors use xUnit assertions instead of fluent chains.
- NSubstitute syntax is the project convention.
- The decision can be revisited without affecting production APIs.

## Alternatives

- **FluentAssertions:** rejected for license fit.
- **Moq:** rejected due to the project's trust posture.

## Related

- [Contributing](../contributing.md)
- [ADR 0012: Four-tier testing](./0012-four-tier-test-strategy.md)
