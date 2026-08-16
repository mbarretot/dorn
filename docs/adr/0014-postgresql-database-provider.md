# 0014. PostgreSQL as a First-Class Database Provider

## Status

Accepted

## Context

ADR 0011 removed manual setup for SQL Server, but PostgreSQL still required post-generation surgery. Existing binary branching also risked silently falling back to SQLite when a third provider appeared.

## Decision

Add `postgres` at parity with SQLite and SQL Server.

- Replace binary branches with explicit `UseSqlite`, `UseSqlServer`, and `UsePostgres` paths.
- Use Npgsql for EF Core and Dapper variants.
- Add Aspire PostgreSQL, Testcontainers, Compose, CI marker, and CLI validation support.
- Check in real PostgreSQL migrations and emit exactly one provider set.
- Reuse `postgres:17` across supported container paths.

Unhandled providers must fail generation, restore, or compilation rather than silently selecting SQLite behavior.

## Consequences

- `--database postgres` is immediately runnable through Aspire or Compose when Docker is available.
- Three migration sets must stay aligned with the model.
- A future provider has a clear exhaustive branching recipe.
- MySQL, Oracle, and other engines remain manual adaptations.

## Alternatives

- **Keep PostgreSQL as a manual swap:** rejected because it repeats the friction ADR 0011 removed.
- **Retain `else = SQLite`:** rejected because new providers could generate incorrect output silently.

## Related

- [ADR 0011: Database selection](./0011-database-provider-selection.md)
- [Web API persistence](../templates/webapi.md#-persistence)
