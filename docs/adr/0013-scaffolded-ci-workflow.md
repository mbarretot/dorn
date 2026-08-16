# 0013. Scaffolded CI Workflow

## Status

Accepted

## Context

Web API generations include four test tiers and provider-specific infrastructure but originally had no CI workflow or SDK pin.

## Decision

Generate `.github/workflows/ci.yml` and `global.json` for every Web API project.

| Concern | Policy |
| --- | --- |
| Triggers | `push`, `pull_request`, `workflow_dispatch` |
| Permissions | `contents: read` |
| Runners | Ubuntu and Windows |
| SDK | Same pinned .NET 10 feature band as Dorn at generation time |
| Tests | Full solution by default; optional tier exclusion for manual runs |
| Coverage | Ubuntu aggregation through ReportGenerator |
| Provider marker | `.github/config/db-provider.txt` |

Container-backed provider setup uses conditional Linux steps, not workflow `services`, because service containers cannot be conditionally attached across the Windows matrix.

## Consequences

- Generated repositories start with a working cross-platform CI baseline.
- New providers add marker-driven steps instead of a new matrix axis.
- SQLite remains Docker-free.
- Windows cannot run container-backed provider integration tests on hosted runners; other tiers still run.
- The generated SDK pin is a snapshot, not an automatic upgrade channel.

## Alternatives

- **No generated CI:** rejected because the test strategy needs an executable default.
- **Database as a matrix axis:** rejected to avoid combinatorial growth.
- **Conditional `services` block:** rejected because GitHub Actions cannot express it safely for this matrix.

## Related

- [ADR 0012: Four-tier tests](./0012-four-tier-test-strategy.md)
- [Web API template](../templates/webapi.md)
