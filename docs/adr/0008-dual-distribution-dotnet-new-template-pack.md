# 0008. Dual Distribution: Standalone `dotnet new` Template Package

## Status

Accepted. Partially superseded by [ADR 0028](./0028-external-template-repos-webapi.md): the packaging
project and `eng/scripts/pack-templates.ps1` moved to `mbarretot/dorn-templates-webapi`, which now packs
and publishes `Dorn.Templates.WebApi` directly; this monorepo vendors the published package back into
`templates/webapi/` instead of packing it from in-repo source. The dual-distribution decision itself
(`dorn new webapi` and `dotnet new install Dorn.Templates.WebApi` as independent entry points) still
holds.

## Context

The Web API template already followed the .NET Template Engine specification, but only `dorn new webapi` could generate it. Users and IDEs also benefit from the standard NuGet template channel.

## Decision

Distribute the same `templates/webapi/` content through two independent entry points:

| Entry point | Template cache |
| --- | --- |
| `dorn new webapi` | Isolated `~/.dorn/template-engine` |
| `dotnet new install Dorn.Templates.WebApi` | Global `~/.templateengine` |

The packaging project lives under `eng/packaging/`, outside the template tree. `eng/scripts/pack-templates.ps1` packs it into `./artifacts`, and CI smoke-tests install, discovery, generation, build, and uninstall.

## Consequences

- Users can generate without installing the Dorn CLI.
- Visual Studio can discover the standard template package.
- Both channels consume one template source, so generated content cannot drift.
- The Web API template must remain self-contained.
- Packaging globs and both entry points require CI coverage.

## Alternatives

- **CLI-only distribution:** rejected because vanilla `dotnet new` is a core access path.
- **Put the packaging project inside the template:** rejected because it would be copied into generated projects.

## Related

- [ADR 0002: Embedded Template Engine](./0002-embed-template-engine-edge.md)
- [ADR 0010: Shared packages](./0010-extract-messaging-and-shared-kernel-as-nuget-packages.md)
