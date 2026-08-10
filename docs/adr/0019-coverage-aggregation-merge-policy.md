# 0019. Coverage Aggregation Merge Policy

## Status

Accepted

## Context

`dorn coverage` runs all 4 test tiers (Application/Integration/Architecture/Functional)
under `dotnet test --collect:"XPlat Code Coverage"`, and each tier writes its own
`TestResults/<tier>/<guid>/coverage.cobertura.xml`. Before this change, `CoverageCommand`
read a single Cobertura file — whichever tier happened to finish writing last — and gated
on that file's root `line-rate` attribute. In practice this meant the reported percentage
depended on tier finishing order, not on actual coverage: a tier with 0% coverage on a
class could "win" by writing last and mask that another tier fully covered the same class,
or vice versa. The gate was measuring one tier's report, not the project's real coverage.

## Decision

`CoverageReporter.MergeCobertura` replaces the single-file read with a merge across all
discovered per-tier reports, and `CoverageCommand` discovers one freshest report per
`TestResults/<tier>/` subtree (ADR-adjacent implementation detail; see `FindCoberturaReports`).

1. **Union coverage, not last-writer-wins.** Line coverage is merged keyed by
   `(filename, folded declaring type)`. For each `(key, line number)` pair, the merged
   `hits` is `Math.Max` across all reports that touched that line. A line counts as
   covered project-wide if *any* tier exercised it — this matches how the 4-tier strategy
   (ADR 0012) is meant to be read: Application/Integration/Architecture/Functional
   together prove coverage, not any single tier alone.
2. **Keyed by filename, not type name alone.** Two files can declare a type with the same
   simple name (for example, EF Core migration pairs: `InitialCreate.cs` and
   `InitialCreate.Designer.cs`). Keying by type name alone would silently merge their
   line numbers together. Keying by `(filename, type)` keeps them as distinct entries.
3. **Compiler-generated nested types fold into their declaring type.** Async state
   machines (`Outer/<Method>d__N`), lambda/closure display classes
   (`Outer/<>c__DisplayClassN_M`), and cached-delegate containers (`Outer/<>c`) are folded
   into `Outer` by stripping any path segment starting with `<`. Their lines are real,
   instrumented code — they are folded for readability in the per-class table, not
   dropped. Genuine nested types (`Outer/Inner`, no `<` prefix) are left as separate rows
   because they are user-authored code with their own meaningful coverage.
4. **Excluded from both the merge and the gate**: any entry whose filename contains an
   `obj/` segment (build intermediates), matches `*.g.cs`/`*.generated.cs` (source
   generators, e.g. OpenAPI XML doc support), contains a `/Migrations/` segment, or
   matches `*.Designer.cs`/`*ModelSnapshot.cs` (EF Core migration scaffolding). None of
   this code is authored or meaningfully exercised by project tests — including it would
   dilute the percentage with code nobody is expected to unit test.
5. **The aggregate percentage is Σ covered lines / Σ total lines over the surviving
   merged entries** — a line-weighted total across the whole project — not an average of
   each report's `line-rate` attribute (which would weight a 10-line class the same as a
   1000-line class) and not the root `line-rate` of any single tier's report.

This is a **visible behavior change**: the number `dorn coverage` reports today differs
from before this change, and it can move in either direction (up or down) depending on
the project. This is by design — the new number reflects actual merged, filtered,
line-weighted coverage instead of an arbitrary single tier's report. The fixed 80%
threshold itself (`CoverageReporter.Threshold`, ADR-preceding decision, unchanged) is
**not** affected by this ADR — only how the numerator and denominator fed into that
threshold are computed.

### Alternatives considered

- **Averaging per-package `line-rate` attributes.** Rejected: unweighted, so a tiny
  fully-covered package could offset a large poorly-covered one.
- **Keeping the root `line-rate` of a re-emitted merged synthetic Cobertura document.**
  Rejected: would require re-emitting XML solely to re-derive a number already
  computable directly from the merged DTO.

## Consequences

- Existing CI pipelines and local runs will see a different `dorn coverage` percentage
  after upgrading, even with no source changes — this is expected and does not indicate a
  regression.
- The per-class table (`Assembly | Class | Coverage % | Covered/Total | Uncovered`,
  `--all` to show every class) is now derived from the same merged, filtered dataset the
  gate uses, so the table and the gate percentage are always consistent with each other.
- Migration/generated-file exclusions mean adding a new generated-file pattern in the
  future requires extending `IsExcluded`, not just `.gitignore` — the exclusion is a
  coverage-reporting concern, not a build concern.
