# 0019. Coverage Aggregation Merge Policy

## Status

Accepted

## Context

`dorn coverage` once read whichever Cobertura file was newest. The reported number therefore depended on test completion order instead of all four tiers.

## Decision

Merge one freshest report per tier before calculating the 80% gate.

| Rule | Policy |
| --- | --- |
| Coverage union | Use maximum hits for each line seen across reports |
| Entry key | Filename + folded declaring type |
| Generated nested types | Fold compiler-generated containers into the declaring type |
| User nested types | Keep as separate rows |
| Aggregate | Covered lines divided by total lines after exclusions |

Exclude:

- `obj/` paths
- `*.g.cs` and `*.generated.cs`
- EF Core migration paths
- `*.Designer.cs` and `*ModelSnapshot.cs`

The per-class table and the threshold use the same merged dataset. `--all` changes table visibility, not the gate.

## Consequences

- Coverage reflects the union of Application, Integration, Architecture, and Functional tiers.
- Results may change after upgrading even when source code does not.
- Filename-aware keys prevent unrelated same-named types from merging.
- New generated-file patterns require an explicit reporter exclusion.

## Alternatives

- **Average report line rates:** rejected because small reports would weigh as much as large ones.
- **Keep last-writer-wins:** rejected because completion order is not evidence.
- **Re-emit synthetic Cobertura:** rejected because the aggregate can be calculated directly.

## Related

- [ADR 0012: Four-tier tests](./0012-four-tier-test-strategy.md)
- [Web API project commands](../templates/webapi.md#-tests-and-project-commands)
