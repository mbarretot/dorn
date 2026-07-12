# templates/shared

Source-of-truth for cross-template building blocks — not a compiled project, not referenced directly by any `.csproj`. Every template that needs these files keeps a physical copy of them (see `templates/README.md` for the sync convention).

## Contents

- `Domain/` — base entity and result types shared by every template's Domain layer.
- `Application/Messaging/` — Dorn's own lightweight mediator (`IRequest`, `ISender`, `IRequestHandler`, `IPipelineBehavior`), a from-scratch MIT-licensed replacement for MediatR.
