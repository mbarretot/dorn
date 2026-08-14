# 0021. Tailwind CSS via the Pinned Standalone CLI, Not Node or a `dotnet tool`

## Status

Accepted

## Context

The Blazor WebAssembly template (`templates/blazor/wasm/`) needs a Tailwind CSS v4 build step.
ADR 0005 established that a generated project builds and runs without Docker or manual
external setup; the CLI itself installs with nothing beyond the .NET SDK. No first-party
Tailwind `dotnet tool` exists, and Node/npm are both extra runtimes this template must not
require.

## Decision

`build/Tailwind.targets` resolves a pinned, SHA-256-checksummed Tailwind CSS v4 standalone
binary per RID (`linux-x64`, `linux-arm64`, `linux-x64-musl`, `linux-arm64-musl`, `macos-x64`,
`macos-arm64`, `windows-x64`) into a user-level cache (`$(DornToolsHome)/tailwindcss/<version>/<rid>/`,
default `~/.dorn/tools`), using only first-party MSBuild tasks (`DownloadFile`, `GetFileHash`,
`Exec`). Resolution order: `DORN_TAILWIND_PATH` override, cache, `PATH`, then download.
Checksum mismatch or an unmapped RID fails the build with an actionable message; there is no
silent skip.

## Consequences

- No Node.js, npm, or third-party `dotnet tool` wrapper is required to build the template.
- The pin, download URL, and checksums are auditable in-repo, not delegated to an unverified
  third-party package.
- A first-party Tailwind `dotnet tool`, if one ships later, is a targets-file swap only.
- Each machine downloads once per pinned version; CI caches the tools directory.
- Air-gapped or corporate environments use `DORN_TAILWIND_PATH` to point at a locally
  provisioned binary.

## Alternatives

- **A community `dotnet tool` Tailwind wrapper:** rejected — every option on NuGet is an
  unverified third-party binary downloader; adopting one moves the same supply-chain risk
  off-repo without removing it.
- **Node + npm:** rejected — contradicts the zero-extra-runtime posture in ADR 0005.
- **Commit the binary:** rejected — platform-specific binaries per RID would bloat the repo
  and go stale silently.

## Related

- [ADR 0005: EF Core + SQLite as default persistence](./0005-ef-core-sqlite-default-persistence.md)
- [Architecture](../architecture.md)
