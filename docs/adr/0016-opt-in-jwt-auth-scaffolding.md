# 0016. Opt-in JWT Authentication Scaffolding

## Status

Accepted

## Context

The Web API template had no authentication. Users need either a self-contained development flow or validation against Microsoft Entra ID, without forcing either on the default project.

## Decision

Add `--auth none|custom|azure-ad` as an independent generation choice.

| Mode | Behavior | Constraint |
| --- | --- | --- |
| `none` | Emits no auth files or configuration | Default |
| `custom` | Seeds one demo user and issues 60-minute JWTs | Requires EF Core |
| `azure-ad` | Validates Entra ID access tokens | No login endpoint or client secret |

### Custom mode

- Use `PasswordHasher<AppUser>`, not the full Identity framework.
- Seed at startup, not through `HasData`, to avoid non-deterministic password hashes in the EF model.
- Read the signing key from user secrets or environment configuration.
- Fail startup outside Development when the key is missing or a placeholder.

### Entra mode

- Use `Microsoft.Identity.Web`, not hand-written JWT validation.
- Configure `Instance`, `TenantId`, and `ClientId`.
- Keep downstream API token acquisition, B2C/CIAM, and credential storage out of scope.

Auth-only files and provider migrations are excluded by template modifiers when not selected.

## Consequences

- Auth-enabled generations boot with working validation paths and no manual wiring.
- Default `none` output stays free of auth code.
- Custom mode is demo and development scaffolding, not a complete identity system.
- Entra mode validates tokens but never issues them.
- Adding an auth axis increases the generation combinations that tests must cover.

## Alternatives

- **Always generate auth:** rejected because many APIs use a different identity boundary.
- **Hand-roll Entra validation:** rejected after audience validation proved easy to misconfigure.
- **EF `HasData` password seed:** rejected because salted hashes change the model between runs.

## Related

- [Web API authentication](../templates/webapi.md#-authentication)
- [ADR 0005: EF Core default](./0005-ef-core-sqlite-default-persistence.md)
