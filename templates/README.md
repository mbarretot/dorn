# Templates

Every scaffolding template Dorn can generate.

## Cross-template building blocks

Code meant to be identical across every template that needs it — the domain base types
(`Entity`, `AggregateRoot`, `Result`) and the custom CQRS mediator — is not copied per
template. It ships as three real NuGet packages under the top-level `packages/`
directory (`Dorn.SharedKernel`, `Dorn.Messaging.Contracts`, `Dorn.Messaging`), consumed
via ordinary `PackageReference`. See `docs/adr/0011-extract-messaging-and-shared-kernel-as-nuget-packages.md`
for the full decision record.

## Layout

- `webapi/` — Clean Architecture Minimal API template.
- `ui/` — placeholder for a future Blazor template.
