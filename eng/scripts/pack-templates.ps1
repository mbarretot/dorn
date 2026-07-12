# eng/scripts/pack-templates.ps1
#
# Packages templates/webapi as a standalone NuGet template package
# (<PackageType>Template</PackageType>) so it can be installed with vanilla
# `dotnet new install Dorn.Templates.WebApi` and discovered in Visual Studio's
# "Create a new project" search, completely independently of the `dorn` CLI tool.
#
# This is the "dual distribution" path: templates/webapi remains installable via
# `dorn new webapi` (the dorn CLI's own embedded Template Engine host) AND via this
# NuGet package (vanilla `dotnet new`). Both channels point at the exact same
# templates/webapi content — this script only adds packaging plumbing, it does not
# fork or duplicate the template itself.
#
# What this script does:
#   1. Runs eng/scripts/check-shared-sync.sh and fails fast if templates/shared and
#      templates/webapi have drifted (a stale physical copy must not ship in the
#      package - see docs/adr/0008-templates-shared-physical-copy-sync.md).
#   2. Runs `dotnet pack` against eng/packaging/Dorn.Templates.WebApi, which is
#      deliberately outside templates/webapi/ so the packaging .csproj itself never
#      gets instantiated into a user's generated project.
#   3. Emits the resulting .nupkg to ./artifacts and prints its path.
#
# See docs/adr/0009-dual-distribution-dotnet-new-template-pack.md for the full
# decision record.
#
# Usage:
#   pwsh eng/scripts/pack-templates.ps1
#   pwsh eng/scripts/pack-templates.ps1 -Version 1.2.3
#
# Works identically on pwsh on GitHub Actions ubuntu-latest/windows-latest and locally
# on macOS/Linux/Windows - all paths are resolved from $PSScriptRoot (repo root),
# never from the caller's current working directory.

[CmdletBinding()]
param(
    [string]$Version = "0.1.0-dev"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$checkSharedSyncScript = Join-Path $repoRoot "eng/scripts/check-shared-sync.sh"
$packagingProject = Join-Path $repoRoot "eng/packaging/Dorn.Templates.WebApi/Dorn.Templates.WebApi.csproj"
$artifactsDir = Join-Path $repoRoot "artifacts"

Write-Host "==> Verifying templates/shared and templates/webapi are in sync..."

& bash $checkSharedSyncScript
if ($LASTEXITCODE -ne 0) {
    Write-Error "check-shared-sync.sh failed (exit code $LASTEXITCODE). templates/shared/ and templates/webapi/ have drifted - fix the drift before packaging, otherwise a stale physical copy would ship inside the NuGet package. See docs/adr/0008-templates-shared-physical-copy-sync.md."
    exit 1
}

Write-Host "==> Packing $packagingProject (version $Version)..."

dotnet pack $packagingProject -c Release "-p:PackageVersion=$Version" -o $artifactsDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet pack failed (exit code $LASTEXITCODE) for $packagingProject."
    exit 1
}

$nupkg = Get-ChildItem -Path $artifactsDir -Filter "Dorn.Templates.WebApi.$Version.nupkg" -ErrorAction SilentlyContinue |
    Select-Object -First 1

if (-not $nupkg) {
    Write-Error "dotnet pack reported success but no matching .nupkg was found under $artifactsDir (expected Dorn.Templates.WebApi.$Version.nupkg)."
    exit 1
}

Write-Host "==> Packed successfully:"
Write-Host $nupkg.FullName

exit 0
