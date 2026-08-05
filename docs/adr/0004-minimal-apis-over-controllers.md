# 0004. Minimal APIs Over Controllers

## Status

Accepted

## Context

ASP.NET Core supports two primary styles for HTTP endpoints: Controllers
(`[ApiController]`, attribute routing) and Minimal APIs (`app.MapGet`/`MapPost`, with
`MapGroup` for shared route prefixes and metadata). The `webapi` template needed to pick
one as its default, since it's a starting point contributors build on directly, not a
menu of options.

Controllers have more mature tooling in some areas (versioning libraries, some
Swagger/OpenAPI integrations) and are familiar from older ASP.NET codebases. Minimal
APIs are the more idiomatic, lower-ceremony current style: `MapGroup` shares a route
prefix, tags, and metadata across a feature's endpoints without a base class.

## Decision

The `webapi` template uses Minimal APIs exclusively, organized with `MapGroup` per
feature area.

- Example: `WebApi/Endpoints/TodoEndpoints.cs` defines
  `MapTodoEndpoints(this IEndpointRouteBuilder)`, grouping all `/api/todos/*` routes
  under `app.MapGroup("/api/todos").WithTags("Todos")` and calling `group.MapPost(...)` /
  `group.MapGet(...)` per operation, injecting `ISender` (ADR 0003) directly into the
  endpoint delegate.

## Consequences

- Less boilerplate per endpoint, and a structure (one static class + one extension
  method per feature) that scales cleanly as features are added.
- `Program.cs` stays a flat composition root: `AddInfrastructure(...)`,
  `AddMediator(...)`, `AddOpenApi()`, then `app.MapTodoEndpoints()` per feature, with no
  controller-discovery configuration needed.
- **Trade-off**: Controllers currently have more mature tooling for API versioning and
  some OpenAPI/Swagger extensions. A contributor with a hard requirement there can add
  Controllers alongside Minimal APIs (ASP.NET Core supports mixing both), but the
  template's own pattern is Minimal APIs with `MapGroup`.
