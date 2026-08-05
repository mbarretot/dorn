# Template: `grpc`

The `grpc` template (short name `dorn-grpc`, identity `Dorn.Templates.Grpc`) generates a
gRPC service in Clean Architecture, using the same from-scratch CQRS mediator as `webapi`
and EF Core over SQLite. Unlike `webapi`, it has a fixed MVP scope (no `--database`,
`--orm`, or `--orchestrator` flags), so the fastest way to see it is a single command:

```bash
dorn new grpc MyService
dotnet run --project src/MyService.AppHost   # starts the Aspire dashboard + the gRPC service
```

This creates `./MyService/` (override with `-o|--output`; pass `--force` to overwrite a
non-empty directory), sourced from `Dorn.Templates.Grpc` and renamed from the template's
`sourceName` (`CleanArchGrpcService`) to your project name throughout files, folders, and
namespaces. `dorn new grpc` automatically runs `dotnet tool restore` after generation
(skip with `--no-restore`), same as `webapi`; see
[Local tool manifest](./webapi.md#local-tool-manifest).

## Scope: a fixed MVP, not a smaller `webapi`

`webapi` lets you choose a database provider, an ORM, and an orchestrator at generation
time (see [Persistence: EF Core, database provider selection](./webapi.md#persistence-ef-core-database-provider-selection)
and [Orchestration](./webapi.md#orchestration-aspire-vs-docker-compose-vs-none)). `grpc`
collapses all three axes to one fixed combination (SQLite + EF Core + Aspire) and
exposes no flag for any of them:

```bash
dorn new grpc MyService                 # SQLite + EF Core + Aspire, the only combination
dorn new grpc MyService -o ./out        # custom output directory
dorn new grpc MyService --force         # overwrite a non-empty directory
dorn new grpc MyService --no-restore    # skip the post-generation `dotnet tool restore`
```

`NewGrpcCommand`/`NewGrpcSettings` (`src/Dorn.Cli/Commands/New/`) accept only `<name>`,
`-o|--output`, `--force`, and `--no-restore`: there is no interactive provider/ORM/
orchestrator prompt to skip, because there is nothing to choose. See ADR
0016 for why the template is scoped this way instead of mirroring `webapi`'s full flag
surface.

## Layers

The generated solution (`<Name>.slnx`, self-contained with its own
`Directory.Build.props`/`Directory.Packages.props`) has five projects under `src/`:

- **`<Name>.Domain`**: entities and domain primitives. `TodoItem` (an `AggregateRoot`)
  and `TodoItemCreatedEvent`, structurally identical to `webapi`'s Domain layer; see
  [Domain events with `INotification`](./webapi.md#domain-events-with-inotification).
- **`<Name>.Application`**: CQRS commands/queries, handlers, and `ValidationBehavior`.
  Depends only on `Domain` and the `Dorn.Messaging.Contracts`/`Dorn.Messaging` NuGet
  packages (ADR 0011). No dependency on EF Core or gRPC.
- **`<Name>.Infrastructure`**: EF Core `DbContext`, a flat (no provider subfolder)
  SQLite migration, and `TodoItemRepository`.
- **`<Name>.Grpc`**: the presentation layer, `Protos/todo.proto`, the generated gRPC
  service base class implemented by `TodoGrpcService`, a `ValidationInterceptor`,
  `Program.cs`. Architecturally equivalent to `webapi`'s `.WebApi` project, a thin
  adapter that maps proto messages to `Dorn.Messaging.Contracts` requests and dispatches
  through `ISender`.
- **`<Name>.AppHost`** / **`<Name>.ServiceDefaults`**: the same Aspire orchestration
  layer `webapi` ships when `--orchestrator aspire` is chosen (see
  [AppHost & ServiceDefaults](./webapi.md#apphost--servicedefaults)); `grpc` always
  generates these two projects, since Aspire is not optional here.

Plus, conditionally (`IncludeTests`, default `true`), four test projects under `tests/`:
`<Name>.Application.Tests`, `<Name>.Integration.Tests`, `<Name>.Architecture.Tests`, and
`<Name>.Functional.Tests`.

## The proto contract: `Protos/todo.proto`

```protobuf
syntax = "proto3";

package todo.v1;
option csharp_namespace = "CleanArchGrpcService.Grpc.Protos";

service TodoService {
  rpc CreateTodoItem (CreateTodoItemRequest) returns (CreateTodoItemResponse);
  rpc GetTodoItems (GetTodoItemsRequest) returns (GetTodoItemsResponse);
}

message CreateTodoItemRequest { string title = 1; }
message CreateTodoItemResponse { string id = 1; }
message GetTodoItemsRequest {}
message GetTodoItemsResponse { repeated TodoItem items = 1; }
message TodoItem { string id = 1; string title = 2; bool is_complete = 3; }
```

Two RPCs are implemented end to end, `CreateTodoItem` and `GetTodoItems`, each dispatching
through the mediator to the same `CreateTodoItemCommand`/`GetTodoItemsQuery` handlers
`webapi` uses. The CQRS layer is identical between the two templates; only the
presentation adapter differs.

The Template Engine's `sourceName` replacement (`CleanArchGrpcService` → your project
name) touches only `option csharp_namespace = "CleanArchGrpcService.Grpc.Protos"`. The
wire package (`package todo.v1;`) is left untouched on purpose: it contains no
project-name token, so replacement stays deterministic and can never produce an invalid
proto identifier regardless of casing in the name you pass to `dorn new grpc`.

```csharp
// Grpc/Services/TodoGrpcService.cs
public sealed class TodoGrpcService(ISender sender) : TodoService.TodoServiceBase
{
    public override async Task<CreateTodoItemResponse> CreateTodoItem(
        CreateTodoItemRequest request,
        ServerCallContext context)
    {
        var id = await sender.Send(
            new CreateTodoItemCommand(request.Title),
            context.CancellationToken);

        return new CreateTodoItemResponse { Id = id.ToString() };
    }

    public override async Task<GetTodoItemsResponse> GetTodoItems(
        GetTodoItemsRequest request,
        ServerCallContext context)
    {
        var items = await sender.Send(new GetTodoItemsQuery(), context.CancellationToken);

        var response = new GetTodoItemsResponse();
        response.Items.AddRange(items.Select(item => new Protos.TodoItem
        {
            Id = item.Id.ToString(),
            Title = item.Title,
            IsComplete = item.IsComplete,
        }));
        return response;
    }
}
```

## Validation

FluentValidation validators are auto-discovered from the Application assembly, same as
`webapi`. gRPC has no HTTP middleware pipeline equivalent to `webapi`'s
`ValidationExceptionHandler`, so validation failures surface through a
`Grpc.Core.Interceptors.Interceptor` instead: `ValidationInterceptor` catches
`FluentValidation.ValidationException` and translates it into an
`RpcException(StatusCode.InvalidArgument, detail)`, where `detail` is a flat
`"property: message; property: message"` string identifying the failing field(s).

## Testing strategy

Four tiers, mirroring `webapi`'s split (see
[`docs/adr/0013-four-tier-test-strategy.md`](../adr/0013-four-tier-test-strategy.md)).
Current counts in `templates/grpc/tests/`:

| Project | Tests | Goal | Database |
|---|---|---|---|
| `Application.Tests` | 9 | Unit: handlers, validators, behaviors, domain entities | None |
| `Integration.Tests` | 2 | Real EF Core persistence via `Database.MigrateAsync()` | SQLite (temp file) |
| `Architecture.Tests` | 6 | Fitness functions: Domain/Application/Infrastructure/Grpc layering | N/A |
| `Functional.Tests` | 3 | RPC round-trip via `GrpcChannel` against `WebApplicationFactory<Program>` | SQLite (temp file) |

The `Functional.Tests` tier needs a small workaround `webapi`'s HTTP-based Functional
tier doesn't: ASP.NET Core's `TestServer` tags every response HTTP/1.1, which
`Grpc.Net.Client` refuses to accept. `TodoGrpcApplicationFactory` installs a
`ResponseVersionHandler` (`DelegatingHandler`) that copies the request's HTTP version onto
the response before returning it, so the in-memory `GrpcChannel` sees the HTTP/2 it
expects.

Repository-level proof of "the template is self-contained and buildable outside this
repo" lives in `templates/tests/GrpcTemplateGenerationTests.cs`, alongside `webapi`'s
equivalent; see [Adding a new template](../contributing.md#adding-a-new-template) for
how that project is wired.

## Running the generated project

```bash
dotnet dev-certs https --trust        # one-time per machine, gRPC requires TLS/HTTP2
dotnet run --project src/MyService.AppHost
```

Aspire is not optional for `grpc`: there is no `--orchestrator` flag, so the AppHost is
always generated and is the only supported way to run the service locally. The AppHost
registers the gRPC project as a plain resource:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
builder.AddProject<Projects.MyService_Grpc>("grpc");
builder.Build().Run();
```

Kestrel is configured HTTPS-only, over both HTTP/1.1 and HTTP/2 on the same endpoint:

```json
{ "Kestrel": { "EndpointDefaults": { "Protocols": "Http1AndHttp2" } } }
```

This is deliberate, not the plain `Http2`-only default `dotnet new grpc` ships: Aspire
injects `ASPNETCORE_URLS` at runtime, which overrides any `Kestrel:Endpoints` you'd
configure directly, so the protocol has to be set via `EndpointDefaults` instead. Pinning
`Http2` alone would break the Aspire dashboard's health probe, since `MapDefaultEndpoints`
is probed over plain `HttpClient`, which defaults to HTTP/1.1. `Http1AndHttp2` over TLS
lets ALPN negotiate HTTP/2 for gRPC calls and HTTP/1.1 for the health endpoint from the
same port. There is no `UseHttpsRedirection()` in `Program.cs` (an HTTP redirect is a 307
response that gRPC clients cannot follow), and no OpenAPI, since OpenAPI has no meaning
for a proto contract.

## What's out of scope for this MVP

- **No `--database`/`--orm`/`--orchestrator` flags.** SQLite + EF Core + Aspire is the
  only supported combination today. See [Scope](#scope-a-fixed-mvp-not-a-smaller-webapi)
  above and ADR 0016.
- **No generated CI workflow.** `webapi`'s `.github/workflows/ci.yml` (see
  [Continuous Integration](./webapi.md#continuous-integration)) depends on the
  database-provider/orchestrator matrix `grpc` doesn't have; a `grpc`-specific workflow is
  deferred until provider/orchestrator parity lands.
- **Two RPCs, not a full CRUD surface.** `CreateTodoItem` and `GetTodoItems` are enough to
  prove the proto-to-mediator dispatch pattern end to end; `UpdateTodoItem`/
  `DeleteTodoItem`/streaming RPCs are not implemented.

## Alternative: vanilla `dotnet new`, without the `dorn` CLI

Unlike `webapi` (`Dorn.Templates.WebApi`, see
[the equivalent `webapi` section](./webapi.md#alternative-vanilla-dotnet-new-without-the-dorn-cli)),
`templates/grpc` is **not yet** packed and published as a standalone NuGet template
package: `eng/scripts/pack-templates.ps1` only packs `Dorn.Templates.WebApi` today.
`dorn new grpc` (the isolated `~/.dorn/template-engine` host) is the only supported way to
generate a `grpc` project right now; a `Dorn.Templates.Grpc` package mirroring the
`webapi` dual-distribution story (ADR 0009) is a future addition, not implemented in this
release.
