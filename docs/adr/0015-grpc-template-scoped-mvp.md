# 0015. gRPC Template as a Scoped MVP

## Status

Accepted

## Context

`webapi` was, until now, Dorn's only template: a Clean Architecture ASP.NET Core Minimal
API project with a choice of database provider (`--database sqlite|sqlserver|postgres`,
ADR 0011, ADR 0014), ORM (`--orm efcore|dapper`), and orchestrator
(`--orchestrator aspire|docker-compose|none`). Contributors asked for a gRPC option;
gRPC was not originally scheduled next (the roadmap listed `ui`), but it exercises a
presentation layer HTTP endpoints don't (binary proto contracts,
`Grpc.Core.Interceptors.Interceptor` instead of exception-handler middleware, HTTP/2-only
transport), a better proof that Dorn's CQRS/mediator/`IApplicationDbContext` layers are
genuinely presentation-agnostic than a second HTTP-based template would have been.

Mirroring `webapi`'s full flag surface here would have meant three choice axes
multiplied across gRPC-specific concerns (proto codegen, HTTP/2 transport,
interceptor-based validation) themselves unproven in this codebase, materially larger
than any single `webapi` axis added in one change before.

## Decision

Ship `templates/grpc/` (short name `dorn-grpc`, identity `Dorn.Templates.Grpc`) as a
**fixed-scope MVP**: SQLite + EF Core + Aspire only, no `--database`, `--orm`, or
`--orchestrator` flag. `NewGrpcCommand`/`NewGrpcSettings`
(`src/Dorn.Cli/Commands/New/`) accept only `<name>`, `-o|--output`, `--force`, and
`--no-restore`.

1. **Every conditional axis collapsed, not ported.** `webapi`'s `#if
   (UseSqlServer)`/`UsePostgres`/`UseSqlite` branching, `Directory.Build.props` `Use*`
   symbols, provider-subfolder migrations, and `Compose.slnx` variant have no equivalent
   here: there is exactly one code path. `template.json` exposes a single symbol,
   `IncludeTests`, and no `sources[0].modifiers` beyond excluding `tests/**`.
2. **The proto wire package stays free of the `sourceName` token.** `Protos/todo.proto`
   declares `package todo.v1;`; the Template Engine's `sourceName` replacement
   (`CleanArchGrpcService` → the user's project name) applies only to `option
   csharp_namespace = "CleanArchGrpcService.Grpc.Protos"`, keeping replacement
   deterministic regardless of the name's casing.
3. **Validation surfaces through a gRPC interceptor, not HTTP middleware.** A single
   `Grpc.Core.Interceptors.Interceptor` (`ValidationInterceptor`) catches
   `FluentValidation.ValidationException` (already thrown by the shared
   `ValidationBehavior<,>` pipeline behavior) and translates it into
   `RpcException(StatusCode.InvalidArgument, detail)`, since gRPC has no equivalent to
   `webapi`'s `AddExceptionHandler<ValidationExceptionHandler>()`.
4. **Aspire overrides `Kestrel:Endpoints` at runtime, so the protocol is set via
   `EndpointDefaults` instead.** Kestrel is pinned to `Http1AndHttp2` through
   `Kestrel:EndpointDefaults:Protocols`, not `Http2` alone (the plain `dotnet new grpc`
   default), because that would break the Aspire dashboard's health probe
   (`MapDefaultEndpoints` is reached over plain HTTP/1.1). `Http1AndHttp2` over TLS lets
   ALPN serve gRPC over HTTP/2 and health over HTTP/1.1 from the same port. The AppHost
   registration (`builder.AddProject<Projects.<Name>_Grpc>("grpc")`) uses the plain
   pattern `webapi` uses; there is no `AspireResourceNameValidator` gate, since the
   Aspire resource name is the hardcoded literal `"grpc"`.
5. **Delivered as nine sequential PRs on a feature-branch chain**, mirroring ADR 0014's
   stacked-PR pattern (each slice independently mergeable, under the 400-line budget):
   template foundation + Domain, Application, Infrastructure + EF Core SQLite, gRPC
   scaffold + `CreateTodoItem` RPC, `GetTodoItems` RPC + Architecture tests, Aspire
   AppHost + ServiceDefaults, CLI command + repository tests, and this documentation
   slice.
6. **Two RPCs implement the same CQRS handlers `webapi` already has.**
   `CreateTodoItem`/`GetTodoItems` dispatch through `ISender` to the identical
   `CreateTodoItemCommand`/`GetTodoItemsQuery` handlers. Domain/Application layers are
   copied structurally unchanged, so the MVP's real surface area is the presentation
   adapter and its tests.

## Consequences

- `dorn new grpc MyService` behaves like `dorn new webapi MyApp` with every optional axis
  already decided: no interactive prompt, no flag to skip, builds and runs immediately
  via `dotnet run --project src/MyService.AppHost`.
- Adding SQL Server/PostgreSQL, Dapper, or `docker-compose`/`none` orchestration later
  means re-introducing the branching this ADR deliberately removed: a bounded,
  well-precedented follow-up (ADR 0011, ADR 0014), not a design change.
- No generated `.github/workflows/ci.yml` ships with `grpc` yet: `webapi`'s CI (ADR 0013)
  is built around a matrix `grpc` doesn't have.
- `templates/grpc` is **not** packed and published as a standalone `dotnet new` NuGet
  template package the way `templates/webapi` is (ADR 0008);
  `eng/scripts/pack-templates.ps1` only packs `webapi` today.
- The RPC surface is intentionally thin: only `CreateTodoItem` and `GetTodoItems`,
  proving the proto-to-mediator dispatch pattern without building out
  `UpdateTodoItem`/`DeleteTodoItem`/streaming RPCs.
- ADR 0011 and ADR 0014 are unaffected: both are `webapi`-only decisions not superseded
  by this ADR.
