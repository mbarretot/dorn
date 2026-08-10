# 0005. EF Core + SQLite as Default Persistence

## Status

Accepted

## Context

A generated service should run immediately. Server databases require provisioning, while SQLite works with a local file and no external process.

## Decision

Use EF Core with SQLite as the Web API default.

- Application owns `IApplicationDbContext`.
- Infrastructure implements the port with `ApplicationDbContext`.
- The default connection is `Data Source=app.db`.
- A real `InitialCreate` migration is generated and applied at startup.

Later ADRs add SQL Server, PostgreSQL, and Dapper choices. SQLite remains the default.

## Consequences

- The default generation builds and runs without Docker or manual schema setup.
- Application remains provider-agnostic.
- SQLite is a development-friendly baseline, not a universal production recommendation.
- Provider-specific migrations must remain separate when another database is selected.

## Alternatives

- **SQL Server or PostgreSQL as the default:** rejected because first run would require external infrastructure.
- **No startup migration:** rejected after verification exposed runtime failures against a missing `Items` table.

## Related

- [ADR 0011: Database selection](./0011-database-provider-selection.md)
- [ADR 0014: PostgreSQL](./0014-postgresql-database-provider.md)
