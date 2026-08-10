# Architecture

Dorn has one job: turn a template into a self-contained .NET service without coupling the generator to the generated runtime.

<p align="center">
  <img src="./images/dorn-architecture.gif" alt="Animated dependency flow from the Dorn CLI through the template engine into a generated service" width="820" />
</p>

## 🧭 System map

| Area | Responsibility | Depends on |
| --- | --- | --- |
| `src/Dorn.Abstractions` | Generation and template contracts | BCL only |
| `src/Dorn.Core` | Template discovery, validation, and instantiation | Abstractions + Template Engine |
| `src/Dorn.Cli` | Commands, prompts, output, and project operations | Core + Abstractions |
| `templates/` | Source for generated Web API, gRPC, and worker services | Published Dorn packages |
| `packages/` | CQRS contracts, mediator runtime, and DDD primitives | Contracts point inward |

The dependency rule is deliberate: Template Engine details stop at `Dorn.Core`, while generated projects never reference `src/`.

## ⚙️ Generation path

1. `Dorn.Cli` validates the command and builds a `GenerationRequest`.
2. `Dorn.Core` discovers templates through an isolated host under `~/.dorn/template-engine`.
3. `TemplateEngineGenerationEngine` instantiates the selected template and maps the result to Dorn contracts.
4. The generated project restores its pinned `Dorn.*` packages and runs independently.

> [!IMPORTANT]
> Dorn checks non-empty output directories before instantiation. Files are overwritten only when `--force` is explicit.

## 📦 Runtime packages

| Package | Owns | Dependency |
| --- | --- | --- |
| `Dorn.Messaging.Contracts` | Requests, handlers, behaviors, notifications, sender, publisher | None |
| `Dorn.Messaging` | Mediator and DI assembly scanning | Messaging.Contracts |
| `Dorn.SharedKernel` | `Entity`, `AggregateRoot`, `Result` | Messaging.Contracts |

Packages are the canonical source. Templates consume them through `PackageReference`, so shared code cannot drift between templates.

## 🏛️ Generated service boundaries

| Layer | May depend on | Must not depend on |
| --- | --- | --- |
| Domain | Shared kernel and contracts | Application, Infrastructure, host |
| Application | Domain and messaging contracts | Infrastructure, host |
| Infrastructure | Application and Domain | Host presentation concerns |
| Host | All inner layers for composition | Nothing points back to it |

The presentation changes by template, but the dependency direction does not:

- **Web API** maps Minimal API endpoints to requests.
- **gRPC** maps proto services to the same request model.
- **Worker** dispatches requests from a timer tick.

## 🔁 Messaging rules

- The first registered `IPipelineBehavior<,>` is the outermost behavior and runs first.
- `Mediator.Send` resolves one matching request handler.
- `Mediator.Publish` invokes every matching notification handler sequentially.
- Aggregate roots raise domain events; Infrastructure publishes them only after persistence succeeds.

## 🧱 Template invariants

- Every template owns its nearest `Directory.Build.props` and `Directory.Packages.props`.
- Generated solutions must build outside this repository.
- Web API options are composed at generation time. gRPC and worker intentionally use fixed MVP profiles.
- `templates/tests` proves generation and standalone compilation from a temporary directory.

## 📚 Decision trail

| Topic | Record |
| --- | --- |
| Embedded Template Engine | [ADR 0002](./adr/0002-embed-template-engine-edge.md) |
| Custom mediator | [ADR 0003](./adr/0003-custom-mediator-instead-of-mediatr.md) |
| Shared NuGet packages | [ADR 0010](./adr/0010-extract-messaging-and-shared-kernel-as-nuget-packages.md) |
| Four-tier testing | [ADR 0012](./adr/0012-four-tier-test-strategy.md) |
| Template guides | [Web API](./templates/webapi.md) · [gRPC](./templates/grpc.md) · [Worker](./templates/worker.md) |
