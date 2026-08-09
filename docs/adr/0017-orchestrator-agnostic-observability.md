# 0017. Orchestrator-Agnostic OpenTelemetry Observability

## Status

Accepted

## Context

`ConfigureOpenTelemetry()`/`AddOpenTelemetryExporters()` shipped only inside
`ServiceDefaults`, called from `Program.cs` behind `#if (UseAspire)`. Choosing
`--orchestrator docker-compose` or `none` (2 of the 3 values) produced a project with zero
logging enrichment, zero traces, zero metrics, contradicting the "production-ready, no
stubs" pitch the same way pre-ADR-0016 auth did. The OTel setup itself was already
orchestrator-agnostic in behavior: the OTLP exporter only activates when
`OTEL_EXPORTER_OTLP_ENDPOINT` is non-empty (fail-open, never blocks startup). It was
Aspire-only purely because of where the code lived.

## Decision

Make OTel wiring unconditional across all three `Orchestrator` values, each adapted to
its own runtime shape, with no new `--observability` flag.

1. **Extraction, not a new axis.** `ConfigureOpenTelemetry()`/`AddOpenTelemetryExporters()`
   move from `ServiceDefaults` (kept Aspire-only: health checks, service discovery,
   resilience) into `WebApi/Extensions/ObservabilityExtensions.cs`, called unconditionally
   from `Program.cs` via `builder.AddObservability()`, before the `#if (UseAspire)`
   block. `HealthEndpointPath`/`AlivenessEndpointPath` are duplicated, not moved:
   `MapDefaultEndpoints()` (Aspire-only, unchanged) still needs its own private copies.
   The 5 OTel `PackageReference`s move from `ServiceDefaults.csproj` to `WebApi.csproj`
   unconditionally, removed from the former to avoid a duplicate reference under
   `aspire`.
2. **`aspire`**: unchanged. `AppHost.cs` still auto-injects `OTEL_EXPORTER_OTLP_ENDPOINT`
   into referenced services with zero explicit code; the dashboard still works exactly as
   before, just via the new unconditional call path.
3. **`docker-compose`**: a real, self-hosted Grafana/Loki/Prometheus/Tempo stack, not the
   Aspire Dashboard. Rejected the dashboard specifically because choosing Compose over
   Aspire signals intent to evaluate tooling outside the Microsoft ecosystem; pointing it
   back at an Aspire-branded UI would defeat that. Data flow:
   `webapi` → OTLP → `otel-collector` → Tempo (native OTLP receiver) / Loki (native
   `/otlp` endpoint; the collector's dedicated `loki` exporter is deprecated) /
   Prometheus (`prometheusremotewrite` + `--web.enable-remote-write-receiver`). Grafana
   is pre-provisioned with all three as datasources, including a `tracesToLogsV2`
   correlation; it never receives telemetry directly. Only `grafana`
   (`${GRAFANA_PORT:-3000}`) and `otel-collector` (4317/4318) publish host ports;
   `loki`/`prometheus`/`tempo` stay internal. All five services use `tmpfs`, not named
   volumes: Docker creates named volumes root-owned, which breaks images running as a
   non-root uid (Tempo and Loki both do); this also matches an ephemeral local-eval
   stack rather than a persistent one. The 3 shared config files
   (`otel-collector-config.yaml`, `tempo.yaml`, `grafana/provisioning/datasources/*.yaml`)
   are DB-agnostic, duplicated into `.SqlServer.yml`/`.Postgres.yml` only as compose
   service blocks referencing the same files, not as separate configs.
4. **`none`**: OTel code present and callable, exporter inert by default (unset env var).
   Deliberately no console exporter fallback: `OpenTelemetry.Exporter.Console` dumps raw
   span/metric objects, not application log lines, and would flood `dotnet run` for the
   orchestrator whose whole value is minimalism. Regular `ILogger` console logging is
   unaffected either way, it's unconditional in ASP.NET Core regardless of this decision.
5. **Image tags resolved against the live Docker Hub API**, not guessed or left at
   `latest`: `otel/opentelemetry-collector-contrib:0.158.0`, `grafana/grafana:13.0.6`,
   `grafana/loki:3.7.6` (clears the `>=3.0` floor needed for native `/otlp/v1/logs`;
   2.x lacks it), `prom/prometheus:v3.13.2`, `grafana/tempo:2.9.4`.

## Consequences

- Every `dorn new webapi MyApp`, regardless of `--orchestrator`, ships the same
  instrumentation code; only the destination differs (Aspire dashboard, self-hosted
  Grafana stack, or nothing until configured).
- `docker-compose` scaffolds grow from 1 service to 6; `docker compose up` is
  meaningfully heavier, a deliberate tradeoff for a real vendor-neutral eval
  environment instead of a thin proxy back to Aspire's own tooling.
- The self-hosted stack is dev/eval-grade by design: anonymous Grafana admin access,
  ephemeral `tmpfs` storage, no authentication on the collector. Documented, not hardened
  for production use.
- Adding a fourth `Orchestrator` value later reuses this recipe: an
  `ObservabilityExtensions` call is already unconditional, only that orchestrator's own
  compose-equivalent (if any) needs its own collector target.
