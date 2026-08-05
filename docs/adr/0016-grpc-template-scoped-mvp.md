# 0016. gRPC Template as a Scoped MVP

## Status

Accepted

## Context

`webapi` was, until now, Dorn's only template: a Clean Architecture ASP.NET Core Minimal
API project with a choice of database provider (`--database sqlite|sqlserver|postgres`,
ADR 0012, ADR 0015), ORM (`--orm efcore|dapper`), and orchestrator
(`--orchestrator aspire|docker-compose|none`). Contributors and end users asked for a gRPC
option, and the roadmap in `README.md` listed `ui` as the next template — gRPC was not
originally scheduled next, but it exercises a presentation layer HTTP endpoints don't
(binary proto contracts, `Grpc.Core.Interceptors.Interceptor` instead of ASP.NET Core
exception-handler middleware, HTTP/2-only transport), which makes it a better proof that
Dorn's CQRS/mediator/`IApplicationDbContext` layers are genuinely presentation-agnostic
than a second HTTP-based template would have been.

Mirroring `webapi`'s full flag surface for a first gRPC template would have meant three
independent choice axes (provider, ORM, orchestrator) multiplied across gRPC-specific
concerns (proto codegen, HTTP/2 transport, interceptor-based validation) that were
themselves unproven in this codebase. That combination was assessed as materially larger
than any single `webapi` axis has ever been added in one change (ADR 0012 added one axis;
ADR 0015 added one value to an existing axis).

## Decision

Ship `templates/grpc/` (short name `dorn-grpc`, identity `Dorn.Templates.Grpc`) as a
**fixed-scope MVP**: SQLite + EF Core + Aspire only, with no `--database`, `--orm`, or
`--orchestrator` flag at all. `NewGrpcCommand`/`NewGrpcSettings`
(`src/Dorn.Cli/Commands/New/`) accept only `<name>`, `-o|--output`, `--force`, and
`--no-restore`.

1. **Every conditional axis collapsed, not ported.** `templates/webapi`'s `#if
   (UseSqlServer)`/`UsePostgres`/`UseSqlite` branching, `Directory.Build.props` `Use*`
   MSBuild symbols, provider-subfolder migrations, and `Compose.slnx` variant all have no
   equivalent in `templates/grpc` — there is exactly one code path, so conditional
   compilation would only add dead branches. `template.json` exposes a single symbol,
   `IncludeTests` (identical in shape to `webapi`'s), and no `sources[0].modifiers` beyond
   the one that excludes `tests/**`.
2. **The proto wire package stays free of the `sourceName` token.**
   `Protos/todo.proto` declares `package todo.v1;` — the Template Engine's `sourceName`
   replacement (`CleanArchGrpcService` → the user's project name) is applied only to
   `option csharp_namespace = "CleanArchGrpcService.Grpc.Protos"`. Putting the project name
   in the wire package would have made replacement depend on the name's casing producing a
   valid proto identifier; keeping the package name-independent makes replacement
   deterministic regardless of what name a user passes to `dorn new grpc`.
3. **Validation surfaces through a gRPC interceptor, not HTTP middleware.** gRPC has no
   equivalent to `webapi`'s `AddExceptionHandler<ValidationExceptionHandler>()`. A single
   `Grpc.Core.Interceptors.Interceptor` (`ValidationInterceptor`) catches
   `FluentValidation.ValidationException` (already thrown by the shared
   `ValidationBehavior<,>` pipeline behavior) and translates it into
   `RpcException(StatusCode.InvalidArgument, detail)`, keeping the same one
   cross-cutting-adapter shape `webapi` uses instead of a per-RPC try/catch.
4. **Kestrel is pinned to `Http1AndHttp2` via `EndpointDefaults`, not per-endpoint.**
   Aspire injects `ASPNETCORE_URLS` at runtime, which overrides any `Kestrel:Endpoints`
   configured directly, so the protocol has to be set through
   `Kestrel:EndpointDefaults:Protocols` instead. Pinning `Http2` alone (the plain `dotnet
   new grpc` default) would break the Aspire dashboard's health probe, since
   `MapDefaultEndpoints` is reached over plain `HttpClient` (HTTP/1.1 by default).
   `Http1AndHttp2` over TLS lets ALPN serve gRPC over HTTP/2 and the health endpoint over
   HTTP/1.1 from the same port. The AppHost registration itself
   (`builder.AddProject<Projects.<Name>_Grpc>("grpc")`) is the plain, unconditional
   pattern `webapi` uses for its own default Aspire path — there is no
   `AspireResourceNameValidator` gate, since the Aspire resource name is the hardcoded
   literal `"grpc"`, not derived from the project name.
5. **Delivered as nine sequential PRs on a feature-branch chain**, mirroring the
   stacked-PR delivery pattern ADR 0015 established for the PostgreSQL provider (each
   slice independently mergeable, kept under the project's 400-changed-line review
   budget): template foundation + Domain, Application, Infrastructure + EF Core SQLite,
   the gRPC scaffold + `CreateTodoItem` RPC, the `GetTodoItems` RPC + Architecture tests,
   the Aspire AppHost + ServiceDefaults, CLI command + repository tests, and this
   documentation slice.
6. **Two RPCs implement the same CQRS handlers `webapi` already has.**
   `CreateTodoItem`/`GetTodoItems` dispatch through `ISender` to the identical
   `CreateTodoItemCommand`/`GetTodoItemsQuery` handlers the `webapi` template ships — the
   Domain/Application layers are copied structurally unchanged, so the MVP's real
   surface area is the presentation adapter and its tests, not a second business-logic
   implementation.

## Consequences

- `dorn new grpc MyService` behaves like `dorn new webapi MyApp` with every optional axis
  already decided: no interactive provider/ORM/orchestrator prompt, no flag to skip,
  builds and runs immediately via `dotnet run --project src/MyService.AppHost`.
- Adding SQL Server/PostgreSQL, Dapper, or `docker-compose`/`none` orchestration to `grpc`
  later means re-introducing the exhaustive branching this ADR deliberately removed — a
  bounded, well-precedented follow-up (ADR 0012, ADR 0015 already did this exercise for
  `webapi`), not a design change.
- No generated `.github/workflows/ci.yml` ships with `grpc` yet: `webapi`'s CI workflow
  (ADR 0014) is built around the database-provider/orchestrator matrix `grpc` doesn't
  have. A `grpc`-specific workflow is deferred to whenever provider/orchestrator parity is
  added, not implemented in this release.
- `templates/grpc` is **not** packed and published as a standalone `dotnet new` NuGet
  template package the way `templates/webapi` is (`Dorn.Templates.WebApi`, ADR 0009) —
  `eng/scripts/pack-templates.ps1` only packs `webapi` today. `dorn new grpc` is the only
  supported generation path until a `Dorn.Templates.Grpc` package is added.
- The RPC surface is intentionally thin: only `CreateTodoItem` and `GetTodoItems` are
  implemented, proving the proto-to-mediator dispatch pattern end to end without building
  out `UpdateTodoItem`/`DeleteTodoItem`/streaming RPCs, which remain future work.
- `docs/adr/0012-database-provider-selection.md` and
  `docs/adr/0015-postgresql-database-provider.md` are unaffected — both describe
  `webapi`-only decisions and are not superseded by this ADR.
