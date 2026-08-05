# Template: `grpc`

The `grpc` template (short name `dorn-grpc`, identity `Dorn.Templates.Grpc`) generates a
gRPC service in Clean Architecture, using the same from-scratch CQRS mediator as `webapi`
and EF Core over SQLite. Unlike `webapi`, it has a fixed MVP scope: no `--database`,
`--orm`, or `--orchestrator` flags.

```bash
dorn new grpc MyService
dotnet run --project src/MyService.AppHost   # starts the Aspire dashboard + the gRPC service
```

This creates `./MyService/` (`-o|--output` to override; `--force` overwrites a non-empty
directory), sourced from `Dorn.Templates.Grpc` and renamed from the template's
`sourceName` (`CleanArchGrpcService`) to your project name throughout. `dorn new grpc`
runs `dotnet tool restore` automatically after generation (skip with `--no-restore`),
same as `webapi`; see [Local tool manifest](./webapi.md#local-tool-manifest).

## Scope: a fixed MVP, not a smaller `webapi`

`webapi` lets you choose a database provider, ORM, and orchestrator at generation time
(see [Persistence](./webapi.md#persistence-ef-core-database-provider-selection) and
[Orchestration](./webapi.md#orchestration-aspire-vs-docker-compose-vs-none)). `grpc`
collapses all three axes to one fixed combination (SQLite + EF Core + Aspire), no flags:

```bash
dorn new grpc MyService                 # SQLite + EF Core + Aspire, the only combination
dorn new grpc MyService -o ./out        # custom output directory
dorn new grpc MyService --force         # overwrite a non-empty directory
dorn new grpc MyService --no-restore    # skip the post-generation `dotnet tool restore`
```

`NewGrpcCommand`/`NewGrpcSettings` (`src/Dorn.Cli/Commands/New/`) accept only `<name>`,
`-o|--output`, `--force`, and `--no-restore`: no interactive provider/ORM/orchestrator
prompt, because there's nothing to choose. See ADR 0015.

## Layers

The generated solution (`<Name>.slnx`, self-contained with its own
`Directory.Build.props`/`Directory.Packages.props`) has five projects under `src/`:

- **`<Name>.Domain`**: entities and domain primitives. `TodoItem` (an `AggregateRoot`)
  and `TodoItemCreatedEvent`, structurally identical to `webapi`'s Domain layer; see
  [Domain events with `INotification`](./webapi.md#domain-events-with-inotification).
- **`<Name>.Application`**: CQRS commands/queries, handlers, and `ValidationBehavior`.
  Depends only on `Domain` and the `Dorn.Messaging.Contracts`/`Dorn.Messaging` packages
  (ADR 0010). No dependency on EF Core or gRPC.
- **`<Name>.Infrastructure`**: EF Core `DbContext`, a flat SQLite migration, and
  `TodoItemRepository`.
- **`<Name>.Grpc`**: the presentation layer: `Protos/todo.proto`, the generated gRPC
  service base class implemented by `TodoGrpcService`, a `ValidationInterceptor`,
  `Program.cs`. Equivalent to `webapi`'s `.WebApi` project: a thin adapter mapping proto
  messages to `Dorn.Messaging.Contracts` requests and dispatching through `ISender`.
- **`<Name>.AppHost`** / **`<Name>.ServiceDefaults`**: the same Aspire layer `webapi`
  ships with `--orchestrator aspire` (see
  [AppHost & ServiceDefaults](./webapi.md#apphost--servicedefaults)); always generated
  here.

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

Two RPCs are implemented end to end, `CreateTodoItem` and `GetTodoItems`, dispatching
through the mediator to the same `CreateTodoItemCommand`/`GetTodoItemsQuery` handlers
`webapi` uses; only the presentation adapter differs between the two templates.

The Template Engine's `sourceName` replacement (`CleanArchGrpcService` → your project
name) touches only `option csharp_namespace = "CleanArchGrpcService.Grpc.Protos"`. The
wire package (`package todo.v1;`) is left untouched on purpose: no project-name token
means replacement stays deterministic and can never produce an invalid proto identifier,
regardless of casing in the name passed to `dorn new grpc`.

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

- FluentValidation validators are auto-discovered from the Application assembly, same as
  `webapi`.
- gRPC has no HTTP middleware pipeline equivalent to `webapi`'s `ValidationExceptionHandler`,
  so validation failures surface through a `Grpc.Core.Interceptors.Interceptor` instead.
- `ValidationInterceptor` catches `FluentValidation.ValidationException` and translates it
  into an `RpcException(StatusCode.InvalidArgument, detail)`, where `detail` is a flat
  `"property: message; property: message"` string identifying the failing field(s).

## Testing strategy

Four tiers, mirroring `webapi`'s split (see
[`docs/adr/0012-four-tier-test-strategy.md`](../adr/0012-four-tier-test-strategy.md)).
Current counts in `templates/grpc/tests/`:

| Project | Tests | Goal | Database |
|---|---|---|---|
| `Application.Tests` | 9 | Unit: handlers, validators, behaviors, domain entities | None |
| `Integration.Tests` | 2 | Real EF Core persistence via `Database.MigrateAsync()` | SQLite (temp file) |
| `Architecture.Tests` | 6 | Fitness functions: Domain/Application/Infrastructure/Grpc layering | N/A |
| `Functional.Tests` | 3 | RPC round-trip via `GrpcChannel` against `WebApplicationFactory<Program>` | SQLite (temp file) |

The `Functional.Tests` tier needs a workaround `webapi`'s HTTP-based tier doesn't:
ASP.NET Core's `TestServer` tags every response HTTP/1.1, which `Grpc.Net.Client`
refuses. `TodoGrpcApplicationFactory` installs a `ResponseVersionHandler`
(`DelegatingHandler`) that copies the request's HTTP version onto the response, so the
in-memory `GrpcChannel` sees the HTTP/2 it expects.

Repository-level proof that the template is self-contained and buildable outside this
repo lives in `templates/tests/GrpcTemplateGenerationTests.cs`, alongside `webapi`'s
equivalent (see [Adding a new template](../contributing.md#adding-a-new-template)).

## Running the generated project

```bash
dotnet dev-certs https --trust        # one-time per machine, gRPC requires TLS/HTTP2
dotnet run --project src/MyService.AppHost
```

Aspire is not optional for `grpc`: with no `--orchestrator` flag, the AppHost is always
generated and is the only supported way to run the service locally. It registers the
gRPC project as a plain resource:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
builder.AddProject<Projects.MyService_Grpc>("grpc");
builder.Build().Run();
```

Kestrel is configured HTTPS-only, over both HTTP/1.1 and HTTP/2 on the same endpoint:

```json
{ "Kestrel": { "EndpointDefaults": { "Protocols": "Http1AndHttp2" } } }
```

This is deliberate, not the plain `Http2`-only default `dotnet new grpc` ships:

- Aspire injects `ASPNETCORE_URLS` at runtime, overriding any direct `Kestrel:Endpoints`
  config, so the protocol must be set via `EndpointDefaults` instead.
- Pinning `Http2` alone would break the Aspire dashboard's health probe:
  `MapDefaultEndpoints` is probed over plain `HttpClient`, which defaults to HTTP/1.1.
- `Http1AndHttp2` over TLS lets ALPN negotiate HTTP/2 for gRPC calls and HTTP/1.1 for the
  health endpoint, from the same port.
- No `UseHttpsRedirection()` in `Program.cs` (an HTTP redirect is a 307 response gRPC
  clients can't follow), and no OpenAPI, since it has no meaning for a proto contract.

## What's out of scope for this MVP

- **No `--database`/`--orm`/`--orchestrator` flags.** SQLite + EF Core + Aspire is the
  only supported combination today. See [Scope](#scope-a-fixed-mvp-not-a-smaller-webapi)
  and ADR 0015.
- **No generated CI workflow.** `webapi`'s `.github/workflows/ci.yml` (see
  [Continuous Integration](./webapi.md#continuous-integration)) depends on a
  database-provider/orchestrator matrix `grpc` doesn't have; deferred until
  provider/orchestrator parity lands.
- **Two RPCs, not a full CRUD surface.** `CreateTodoItem` and `GetTodoItems` prove the
  proto-to-mediator dispatch pattern end to end; `UpdateTodoItem`/`DeleteTodoItem`/
  streaming RPCs aren't implemented.

## Alternative: vanilla `dotnet new`, without the `dorn` CLI

Unlike `webapi` (see [the equivalent
section](./webapi.md#alternative-vanilla-dotnet-new-without-the-dorn-cli)),
`templates/grpc` is **not yet** packed as a standalone NuGet template package
(`eng/scripts/pack-templates.ps1` only packs `Dorn.Templates.WebApi`). `dorn new grpc`
(the isolated `~/.dorn/template-engine` host) is the only way to generate one right now;
a `Dorn.Templates.Grpc` package (ADR 0008) isn't implemented yet.
