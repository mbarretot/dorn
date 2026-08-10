# 0017. Orchestrator-Agnostic OpenTelemetry Observability

## Status

Accepted

## Context

OpenTelemetry originally lived in Aspire-only `ServiceDefaults`. Compose and plain generations therefore lost logs enrichment, traces, and metrics even though exporter activation already depended only on `OTEL_EXPORTER_OTLP_ENDPOINT`.

## Decision

Move instrumentation into an unconditional Web API extension and adapt only the destination:

| Orchestrator | Destination |
| --- | --- |
| Aspire | Aspire dashboard through injected OTLP configuration |
| Docker Compose | OTel Collector to Grafana, Loki, Prometheus, and Tempo |
| None | User-provided OTLP endpoint; inert when unset |

Aspire-specific health, discovery, and resilience remain in `ServiceDefaults`.

The Compose stack is vendor-neutral and local-only. It uses ephemeral `tmpfs`, keeps backends internal, and exposes Grafana plus the collector.

## Consequences

- Every Web API generation has the same instrumentation hooks.
- Compose grows into a heavier six-service local evaluation stack.
- The stack is intentionally not production-hardened.
- Plain runs remain quiet until an exporter endpoint is configured.
- Regular `ILogger` console output remains independent of OTLP.

## Alternatives

- **Keep Aspire-only telemetry:** rejected because orchestration should not decide instrumentation.
- **Use Aspire Dashboard from Compose:** rejected because Compose is the non-Aspire evaluation path.
- **Console exporter fallback:** rejected because raw telemetry would overwhelm the minimal run experience.

## Related

- [Web API observability](../templates/webapi.md#-observability)
- [ADR 0013: Scaffolded CI](./0013-scaffolded-ci-workflow.md)
