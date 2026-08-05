# 0006. xUnit + NSubstitute Over FluentAssertions/Moq

## Status

Accepted

## Context

Every test project in this repo (`tests/Dorn.Core.Tests`, `tests/Dorn.Cli.Tests`,
`templates/tests`, and `webapi`'s own `tests/CleanArchWebApi.Application.Tests`) needs a
test framework, an assertion style, and a mocking library. xUnit is the test framework
across the board; the two libraries considered for assertions and mocking each carry a
complication:

- **FluentAssertions** moved to a commercial license starting with v8 (January 2025),
  not a sustainable default for a template contributors build on for years.
- **Moq** shipped a 2023 version that silently added telemetry (`SponsorLink`)
  collecting developer email hashes without clear consent. Since reverted, but it left a
  lasting perception risk.

## Decision

Every test project uses **xUnit** for the test framework, plain xUnit `Assert.*` calls
(no fluent assertion library), and **NSubstitute** where test doubles are needed
(currently: `Dorn.Cli.Tests` substitutes `IGenerationEngine` to verify `NewWebApiCommand`
builds the expected `GenerationRequest` without invoking the real Template Engine).

## Consequences

- No dependency in this repo or the `webapi` template carries a commercial license or a
  known telemetry incident, consistent with Dorn's MIT-license, low-friction goal (ADR
  0003).
- Assertions read as plain `Assert.Equal(...)`/`Assert.True(...)` rather than fluent
  `.Should().Be(...)` chains, accepted as the cost of avoiding FluentAssertions's
  licensing.
- NSubstitute's API (`Substitute.For<T>()`, `.Returns(...)`, `.Received()`) differs
  syntactically from Moq's, so contributors from a Moq background need a short
  adjustment.
- If FluentAssertions or Moq's situation changes, this decision can be revisited per test
  project independently, since it doesn't leak into production code.
