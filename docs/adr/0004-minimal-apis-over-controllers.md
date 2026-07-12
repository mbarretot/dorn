# 0004. Minimal APIs Over Controllers

## Status

Accepted

## Context

ASP.NET Core supports two primary styles for defining HTTP endpoints: MVC-style
Controllers (`[ApiController]`, action methods, attribute routing) and Minimal APIs
(`app.MapGet`/`MapPost`/etc., optionally organized with `MapGroup` for shared route
prefixes and metadata). The `webapi` template needed to pick one as its default endpoint
style, since it's meant to be a starting point contributors and their teams build on
directly, not a menu of options to choose between at generation time.

Controllers bring more mature tooling in some areas — versioning libraries and some
Swagger/OpenAPI integrations historically assumed a Controller-based project shape — and
are more familiar to developers coming from older ASP.NET/ASP.NET Core codebases. Minimal
APIs are the more idiomatic, lower-ceremony style in current ASP.NET Core, with less
boilerplate per endpoint (no controller class, no `[HttpGet]`/`[FromBody]` attribute
scaffolding) and `MapGroup` giving a clean way to share a route prefix, tags, and other
metadata across a feature's endpoints without a base class.

## Decision

The `webapi` template uses Minimal APIs exclusively, organized with `MapGroup` per
feature area. For example, `WebApi/Endpoints/TodoEndpoints.cs` defines
`MapTodoEndpoints(this IEndpointRouteBuilder)`, which groups all `/api/todos/*` routes
under `app.MapGroup("/api/todos").WithTags("Todos")` and calls `group.MapPost(...)` /
`group.MapGet(...)` for each operation, injecting `ISender` (see ADR 0003) directly into
the endpoint delegate.

## Consequences

- Less boilerplate per endpoint, and a project structure (one static class + one
  extension method per feature) that scales cleanly as features are added — this is the
  same structure a contributor extending the template with a new feature should follow.
- `Program.cs` stays a flat composition root: `builder.Services.AddInfrastructure(...)`,
  `AddMediator(...)`, `AddOpenApi()`, then `app.MapTodoEndpoints()` and so on per feature,
  with no controller-discovery/attribute-routing configuration needed.
- Trade-off, documented here rather than silently accepted: Controllers currently have
  more mature tooling for API versioning and some third-party OpenAPI/Swagger extensions.
  A contributor with a hard requirement on that tooling can still add Controllers
  alongside Minimal APIs in a generated project — ASP.NET Core supports mixing both in
  the same app — but the template itself, and the pattern new features are expected to
  follow, is Minimal APIs with `MapGroup`.
