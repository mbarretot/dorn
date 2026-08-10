# 0011. Database Provider Selection at Generation Time

## Status

Accepted

## Context

SQLite is the best zero-setup default, but choosing SQL Server should not require manual package, registration, migration, and orchestration edits after generation.

## Decision

Add `--database sqlite|sqlserver` to the Web API template.

| Area | Generated behavior |
| --- | --- |
| Infrastructure | Select `UseSqlite` or `UseSqlServer` |
| AppHost | Add and reference a SQL Server resource when selected |
| Packages | Include only the selected provider and Aspire integration |
| Configuration | Keep a static SQLite connection or receive the Aspire connection |
| Migrations | Emit exactly one provider-specific migration set |
| CLI | Prompt interactively; default to SQLite non-interactively |

ADR 0014 later adds PostgreSQL through the same pattern.

## Consequences

- Default generation remains zero-setup SQLite.
- SQL Server generation builds without Docker but needs Docker to run its Aspire database.
- Provider-specific migrations add a small maintenance cost.
- `IApplicationDbContext` remains provider-independent.

## Alternatives

- **Manual provider swap:** rejected because it undermines the scaffolding happy path.
- **Make SQL Server the default:** rejected because it requires external infrastructure.

## Related

- [ADR 0005: SQLite default](./0005-ef-core-sqlite-default-persistence.md)
- [ADR 0014: PostgreSQL](./0014-postgresql-database-provider.md)
