# 0016. Opt-in JWT Authentication Scaffolding

## Status

Accepted

## Context

`webapi`-generated projects shipped with zero authentication: `Program.cs` had no
`UseAuthentication()`/`UseAuthorization()`, and every endpoint was open. Protecting an
endpoint required hand-wiring JWT bearer auth from scratch, contradicting the
"production-ready, no stubs" pitch. Two real-world needs exist and neither subsumes the
other: teams that want a self-contained demo/dev auth flow with no external dependency,
and teams already on Microsoft Entra ID that just need token validation.

## Decision

Add `Auth` as a third orthogonal `--auth none|custom|azure-ad` choice
(`dorn new webapi MyApp --auth custom`), mirroring the `Orchestrator`/`DatabaseProvider`
symbol → validator → CLI flag → `#if`/`template.json`-modifier recipe verbatim. Default
`none` emits zero new files, byte-identical to pre-ADR-0016 output.

1. **`custom`: self-issued JWT, no external identity provider.** `PasswordHasher<AppUser>`
   (`Microsoft.Extensions.Identity.Core`) only, not the full ASP.NET Core Identity
   framework (no `UserManager`, no cookie auth, no Identity Core tables). A single seeded
   demo user (`AuthSeeder`, `AnyAsync` idempotency guard) runs at **application startup**,
   not via EF `HasData`: a live `PasswordHasher` call inside `OnModelCreating` re-salts
   per process start, and EF 10 promotes `PendingModelChangesWarning` to an error, so a
   non-deterministic `HasData` value breaks `MigrateAsync()` at every boot. `POST
   /auth/login` issues a 60-minute JWT through the existing custom mediator. The signing
   key is never committed: `UserSecretsId` added to `CleanArchWebApi.WebApi.csproj`
   (previously only `AppHost` had one), `Jwt:SigningKey` fails fast
   (`InvalidOperationException`) when missing/placeholder outside `Development`.
2. **`azure-ad`: validation-only against Microsoft Entra ID, via `Microsoft.Identity.Web`.**
   `AddAuthentication(...).AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"))`
   needs only `AzureAd:Instance`/`TenantId`/`ClientId`, no client secret: this flavor never
   acts as a client, so `EnableTokenAcquisitionToCallDownstreamApi()` (the one path that
   needs a secret) is an explicit non-goal. No `/auth/login` endpoint: Entra ID issues
   tokens directly to external clients. An earlier implementation pass hand-rolled
   `AddJwtBearer` instead of using `Microsoft.Identity.Web`; that version never set
   `TokenValidationParameters.ValidAudience`, and since `ValidateAudience` defaults to
   `true`, it rejected every real token. Caught and replaced with the real library before
   merge (see PR #54's commit history for the verification trail).
3. **`custom` requires `Orm=efcore`; the guard lives in the CLI command, not the
   validator.** `AuthValidator` stays cross-symbol-free (same shape as
   `OrchestratorValidator`); the compatibility check is in `NewWebApiCommand.RunAsync`
   after both values resolve, mirroring the existing `aspire`+non-`sqlite` guard. The
   Dapper path excludes `Migrations/**`/`ApplicationDbContext.cs` entirely, so a seeded EF
   user has nowhere to live under `Orm=dapper`.
4. **Migration footprint: additive per-provider, conditional snapshot only.** One
   `{timestamp}_AddAuthUser` migration + `.Designer.cs` per provider (Sqlite, SqlServer,
   Postgres); the three `InitialCreate` migrations stay byte-identical. Only
   `ApplicationDbContextModelSnapshot.cs` (×3) gains an `#if (UseCustomAuth)` block.
5. **Both endpoints (`/api/me`, `/auth/login`) and every auth-only file are excluded from
   `Auth=none`/`Auth=azure-ad` scaffolds via `template.json` modifiers**, not left as
   dead `#if`-wrapped files: `AppUser.cs`, `LoginCommand*.cs`, `ITokenService.cs`,
   `JwtTokenService.cs`, `AuthSeedOptions.cs`, `AuthSeeder.cs`, and the six migration
   files are all `!UseCustomAuth`-excluded, on top of the shared `!UseAuth` exclusions
   (`AuthenticationExtensions.cs`, `MeEndpoints.cs`).
6. **CI-safe testing for both flavors, without a live Entra ID.** `custom`'s
   `AuthWebApplicationFactory` overrides the signing key via `WebApplicationFactory`
   settings. `azure-ad` needed a different approach:
   `Microsoft.Identity.Web` installs its own dynamic `IssuerValidator`/`AudienceValidator`
   delegates for real AAD multi-tenant matching, which take precedence over any static
   override and proved unsafe to fake against the library's internals. Instead,
   `AzureAdWebApplicationFactory` swaps the default authentication scheme for a
   minimal test-only `AuthenticationHandler` that independently verifies the same
   HS256-signed token format the tests issue, fully decoupled from
   `Microsoft.Identity.Web`. Validating that library's own correctness is its
   maintainers' responsibility, not this scaffold's.

## Consequences

- `dorn new webapi MyApp --auth custom|azure-ad` produces a project that boots, seeds
  (custom), and serves a real login/claims round-trip with zero manual wiring.
- `Auth=none` (default) is unaffected: no new files, no new `Program.cs` lines, no new
  `appsettings.json` keys.
- A third provider-agnostic `--auth` axis composes freely with `--database`/`--orm`/
  `--orchestrator`, following the same recipe a fourth axis would reuse.
- `custom` is demo/dev-grade by design (one seeded user, no roles/claims model, no
  refresh tokens); teams needing more build on `ITokenService`/`AppUser` rather than
  hand-wiring from scratch.
- `azure-ad` is validation-only: no downstream API calls, no token caching, no B2C/CIAM
  support. `Microsoft.Identity.Web`'s own advanced features (`.EnableTokenAcquisitionToCallDownstreamApi()`,
  certificate credentials) remain available to a consumer who needs them, not wired here.
