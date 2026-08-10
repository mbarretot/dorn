# 0015. gRPC Template as a Scoped MVP

## Status

Accepted

## Context

A gRPC template proves that Dorn's Application layer works behind a binary protocol. Copying every Web API option at once would combine unproven transport concerns with three configuration axes.

## Decision

Ship `dorn-grpc` with one fixed profile:

| Concern | Choice |
| --- | --- |
| Persistence | EF Core + SQLite |
| Orchestration | Aspire |
| RPCs | `CreateTodoItem`, `GetTodoItems` |
| Validation | gRPC interceptor maps failures to `InvalidArgument` |
| Protocols | TLS with `Http1AndHttp2` |

The command accepts name, output, force, and no-restore only. The proto package remains `todo.v1`; source-name replacement changes the C# namespace, not the wire identifier.

HTTP/1.1 remains enabled for Aspire health probes while gRPC negotiates HTTP/2 through ALPN.

## Consequences

- `dorn new grpc MyService` has no configuration prompt and runs through AppHost.
- The same CQRS handlers work behind a different presentation adapter.
- Database, ORM, and orchestrator choices are deferred.
- CI scaffolding, standalone template packaging, CRUD expansion, and streaming are deferred.

## Alternatives

- **Full Web API option parity:** rejected to keep the first gRPC slice bounded.
- **HTTP/2 only:** rejected because Aspire health probes also need HTTP/1.1.

## Related

- [gRPC template](../templates/grpc.md)
- [ADR 0012: Four-tier tests](./0012-four-tier-test-strategy.md)
