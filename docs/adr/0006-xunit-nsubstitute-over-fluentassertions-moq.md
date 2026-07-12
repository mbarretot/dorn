# 0006. xUnit + NSubstitute Over FluentAssertions/Moq

## Status

Accepted

## Context

Every test project in this repo (`tests/Dorn.Core.Tests`, `tests/Dorn.Cli.Tests`,
`tests/Templates.Tests`, and the `webapi` template's own
`tests/CleanArchWebApi.Application.Tests`) needs a test framework, an assertion style,
and — where mocking is needed (`Dorn.Cli.Tests` fakes `IGenerationEngine`) — a mocking
library. xUnit is the test framework across the board; the two libraries under
consideration for assertions and mocking each carry a licensing or reputational
complication:

- **FluentAssertions**, historically MIT-licensed and near-ubiquitous for fluent-style
  `result.Should().Be(...)` assertions in .NET tests, moved to a commercial license
  starting with v8 (January 2025). A project depending on FluentAssertions v8+ either
  needs a paid license or must pin to the last free version, which is not a sustainable
  default for a template contributors will build on for years.
- **Moq**, the most widely used .NET mocking library, shipped a version in 2023 that
  silently added a dependency (`SponsorLink`) which collected and transmitted developer
  email hashes without clear consent — a telemetry-collection controversy that, while
  since resolved (the offending release was reverted), damaged community trust and left
  a lasting perception risk for a project that wants no friction adopting its default
  dependencies.

## Decision

Every test project in this repo uses **xUnit** for the test framework, plain xUnit
`Assert.*` calls for assertions (no fluent assertion library), and **NSubstitute** where
test doubles are needed (currently: `Dorn.Cli.Tests` substitutes `IGenerationEngine` to
verify `NewWebApiCommand` builds the expected `GenerationRequest` without invoking the
real Template Engine).

## Consequences

- No dependency in this repo or in the `webapi` template carries a commercial license or
  a known telemetry-collection incident — consistent with the MIT-license, low-friction
  goal behind ADR 0007 and ADR 0003 (custom mediator for the same reason).
- Assertions read as plain `Assert.Equal(...)`/`Assert.True(...)` rather than fluent
  `.Should().Be(...)` chains — a smaller, less expressive assertion vocabulary than
  FluentAssertions offers, accepted as the cost of avoiding its licensing.
- NSubstitute's substitute-based API (`Substitute.For<T>()`, `.Returns(...)`,
  `.Received()`) differs syntactically from Moq's `Mock<T>`/`.Setup(...)`/`.Verify(...)`
  — contributors coming from a Moq background need a short adjustment, documented
  implicitly by the existing test code in `tests/Dorn.Cli.Tests` as the pattern to follow.
- If FluentAssertions or Moq's situation changes in the future (a return to a permissive
  license, for instance), this decision can be revisited per test project independently,
  since assertion/mocking choice doesn't leak into production code.
