# 0002. Embed Microsoft.TemplateEngine.Edge Instead of Shelling Out to `dotnet new`

## Status

Accepted

## Context

Dorn can shell out to `dotnet new` or run the same Template Engine in-process. Shelling out is initially simpler, but it mutates the user's global template cache and returns only process output.

## Decision

Embed `Microsoft.TemplateEngine.Edge` behind Dorn-owned contracts.

| Component | Responsibility |
| --- | --- |
| `DornTemplateEngineHost` | Isolated settings under `~/.dorn/template-engine` |
| `FileSystemTemplateCatalog` | Scan source templates through `Scanner` |
| `TemplateEngineGenerationEngine` | Instantiate through `TemplateCreator` |
| `IGenerationEngine`, `ITemplateCatalog` | Keep Template Engine types out of CLI consumers |

The scanned mount stays alive for the process because template content is read lazily.

## Consequences

- Dorn never changes `~/.templateengine` or pollutes `dotnet new --list`.
- Generation returns typed results and is testable in-process.
- Template Engine API changes are isolated to `Dorn.Core`.
- The package surface is SDK-coupled and must be upgraded with care.

## Alternatives

- **Shell out to `dotnet new`:** rejected due to global state, untyped output, and weaker tests.
- **Use older `Bootstrapper` examples:** rejected because that facade is absent from the pinned SDK surface.

## Related

- [ADR 0001: .NET 10](./0001-target-framework-net10.md)
- [Architecture](../architecture.md)
