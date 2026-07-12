# Dorn

Dorn is a community-driven .NET scaffolding CLI, similar in spirit to `dotnet new`, but built around opinionated Clean Architecture templates. The first template, `webapi`, generates a CQRS-based Minimal API project with EF Core + SQLite persistence and zero commercial-licensed dependencies.

## Why Dorn

Most scaffolding tools give you either someone else's opinionated architecture with no way out, or an empty shell that saves you five minutes of clicking. Dorn generates a properly layered Clean Architecture project — Domain, Application, Infrastructure, WebApi — wired end to end, not just stubbed out.

A couple of decisions are worth flagging up front, because they shape everything downstream. MediatR went commercial with v13, so the CQRS plumbing in every generated project is a small mediator we wrote ourselves instead: MIT-licensed, no invoice waiting for your team down the line. Same reasoning kept FluentAssertions and Moq out of the test projects — xUnit + NSubstitute instead. And rather than shelling out to `dotnet new` as a subprocess, Dorn embeds `Microsoft.TemplateEngine.Edge` directly, so it never touches your global template cache and you get real structured results instead of parsed stdout.

Every non-obvious call like these is written down as an ADR in [`docs/adr/`](./docs/adr) — read those before assuming a choice was arbitrary.

`webapi` is the first template. `ui` (Blazor) is next; new templates plug into the same catalog and generation pipeline once they land.

## Quickstart

```bash
dotnet tool install -g dorn   # coming soon — not yet published
dorn new webapi MyApp
cd MyApp
dotnet build
```

### Alternative: plain `dotnet new`, no `dorn` tool required

The `webapi` template is also distributed as a standalone NuGet template package, so you
can generate a project with vanilla `dotnet new` — no `dorn` install needed. This is also
what makes the template discoverable in Visual Studio's "Create a new project" search.

```bash
pwsh eng/scripts/pack-templates.ps1   # builds ./artifacts/Dorn.Templates.WebApi.*.nupkg
dotnet new install ./artifacts/Dorn.Templates.WebApi.*.nupkg
dotnet new dorn-webapi -n MyApp
```

Not yet published to NuGet.org (same "coming soon" status as the `dorn` tool above), so
for now this installs from a locally built package. See
[`docs/templates/webapi.md`](./docs/templates/webapi.md) and
[ADR 0009](./docs/adr/0009-dual-distribution-dotnet-new-template-pack.md) for details.

## Documentation

Full documentation, including architecture decisions and ADRs, lives in [`docs/`](./docs):

- [Getting started guide](./docs/getting-started.md)
- [Architecture overview](./docs/architecture.md)
- [Template reference: `webapi`](./docs/templates/webapi.md)
- [Architecture Decision Records (ADRs)](./docs/adr)

## Contributing

See [`docs/contributing.md`](./docs/contributing.md) for how to add a new template, coding conventions, and the verification loop to run before opening a PR.

## License

[MIT](./LICENSE)
