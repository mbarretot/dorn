# 0012. Four-Tier Test Strategy for the `webapi` Template

## Status

Accepted

## Context

One Application test project could not prove migrations, layer boundaries, or transport behavior.

## Decision

Every tested template uses four distinct tiers:

| Tier | Primary proof |
| --- | --- |
| Application | Handlers, validation, behaviors, and domain logic |
| Integration | Selected persistence and real migrations or SQL |
| Architecture | Dependency rules with ArchUnitNET |
| Functional | Host and presentation pipeline round trip |

For Web API generations, Functional tests force SQLite because transport correctness is separate from provider fidelity. Integration tests own provider-specific proof and may use Testcontainers.

Architecture rules use `TngTech.ArchUnitNET.xUnit`; referenced dependencies must be loaded so rules cannot pass vacuously.

## Consequences

- Generated services prove more than compilation.
- Integration is the only tier that may require Docker.
- Functional tests stay fast and provider-independent.
- Four projects increase restore and execution cost.
- Architecture tests are slower than simpler reflection-only checks but produce stronger dependency evidence.

## Alternatives

- **Single test project:** rejected because concerns and infrastructure requirements become blurred.
- **NetArchTest.Rules:** replaced after stale releases and weaker dependency loading made rules less reliable.

## Related

- [ADR 0006: Test libraries](./0006-xunit-nsubstitute-over-fluentassertions-moq.md)
- [ADR 0019: Coverage merge](./0019-coverage-aggregation-merge-policy.md)
