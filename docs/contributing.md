# Contributing

Keep changes focused, preserve template self-containment, and run the same checks CI runs before opening a pull request.

## ⚡ Contribution path

1. Make one reviewable change.
2. Update tests and documentation with the code they explain.
3. Pack local Dorn packages.
4. Build and test the full solution.
5. Open a structured pull request.

## 🧩 Add a template

| Template | Current role |
| --- | --- |
| `webapi` | Configurable reference implementation |
| `grpc` | Fixed SQLite + EF Core + Aspire service |
| `worker` | Fixed SQLite + EF Core + Aspire background service |
| `ui` | Placeholder for future work |

Use this checklist for a new template:

- [ ] Add `templates/<name>/.template.config/template.json` with identity, `shortName`, `sourceName`, and symbols.
- [ ] Add self-contained `Directory.Build.props` and `Directory.Packages.props`. Never inherit the repository root files.
- [ ] Reference `Dorn.SharedKernel`, `Dorn.Messaging.Contracts`, or `Dorn.Messaging` instead of copying shared code.
- [ ] Add the template projects to `Dorn.slnx`.
- [ ] Add a generation test that writes outside the repository and runs `dotnet build` on the result.
- [ ] Add and register `New<Name>Command` under `src/Dorn.Cli/Commands/New/`.
- [ ] Add `docs/templates/<name>.md` with the generated shape, run path, tests, and limits.

## 📐 Repository conventions

| Rule | Required practice |
| --- | --- |
| Package versions | Put versions in the nearest `Directory.Packages.props`; never add inline `Version` attributes to `PackageReference` |
| Transitive override | Add both a central `PackageVersion` and a direct versionless `PackageReference` |
| CQRS | Use `Dorn.Messaging.Contracts` and `Dorn.Messaging`; do not add MediatR |
| Tests | Use xUnit, plain `Assert.*`, and NSubstitute; do not add FluentAssertions or Moq |
| Language | Use English in code, comments, and documentation |

The direct reference in a transitive override is essential. Central Package Management cannot force a transitive version until NuGet treats that package as direct.

## ✅ Verify before a PR

Run in this order:

```bash
pwsh eng/scripts/pack-packages.ps1
dotnet build Dorn.slnx -c Release
DORN_TEMPLATES_PATH="$(pwd)/templates" DORN_LOCAL_NUGET_FEED="$(pwd)/artifacts" dotnet test Dorn.slnx
```

> [!IMPORTANT]
> `pack-packages.ps1` must run first. Raw templates and generation tests restore the local Dorn packages from `./artifacts`.

CI runs the reusable build and test matrix on Ubuntu and Windows.

## 📝 Pull request contract

Use an emoji plus a conventional-commit-style title:

| Emoji | Type | Example |
| --- | --- | --- |
| ✨ | `feat` | `✨ feat: opt-in JWT auth for the webapi template` |
| 🐛 | `fix` | `🐛 fix: audience validation missing on azure-ad tokens` |
| 📚 | `docs` | `📚 docs: ADR 0017 + observability template reference` |
| ♻️ | `refactor` | `♻️ refactor: comment cleanup round 7` |
| 🔀 | `merge` | `🔀 merge: develop → main` |

Structure the description for scanning, not prose:

```markdown
## 🎯 What & Why
State what changed and why in one or two sentences.

## 📦 What's Included
| Area | Change |
| --- | --- |
| `AreaName` | Concrete change |

## ✅ Verification
- [x] `dotnet build` completed
- [x] Relevant tests passed

## 📊 Stats
| Metric | Value |
| --- | --- |
| Files | N |
| Lines | +N / -N |
```

Keep all four sections, including both tables, even for a small documentation change.

## 🚀 Release safety

Release tags use the Linux and Windows test gate before NuGet Trusted Publishing. Do not rename `.github/workflows/publish.yml`; the NuGet policy is bound to that workflow. See [Engineering](../eng/README.md) and [ADR 0020](./adr/0020-nuget-trusted-publishing-and-test-gated-releases.md).

## ⚖️ License

Dorn is [MIT licensed](../LICENSE). Contributions are licensed under the same terms.
