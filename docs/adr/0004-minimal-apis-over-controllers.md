# 0004. Minimal APIs Over Controllers

## Status

Accepted

## Context

The Web API template needs one default HTTP style. Controllers offer mature conventions, while Minimal APIs reduce ceremony and fit feature-oriented endpoint groups.

## Decision

Use Minimal APIs with one `MapGroup` extension per feature.

```text
Program.cs
  -> MapTodoEndpoints()
    -> /api/todos group
      -> endpoint delegate
        -> ISender
```

Endpoints inject `ISender` directly and keep `Program.cs` as the composition root.

## Consequences

- Feature endpoints need less framework boilerplate.
- Shared route metadata stays close to the feature.
- No controller discovery is required.
- Controller-first versioning and some OpenAPI extensions may be easier in controller-based projects.

## Alternatives

- **Controllers:** rejected as the template default, but generated projects may add them alongside Minimal APIs.

## Related

- [Web API template](../templates/webapi.md)
- [ADR 0003: Custom mediator](./0003-custom-mediator-instead-of-mediatr.md)
