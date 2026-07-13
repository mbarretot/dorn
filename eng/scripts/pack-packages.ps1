# eng/scripts/pack-packages.ps1
#
# Packages Dorn's own first-party library packages -- Dorn.Messaging.Contracts,
# Dorn.Messaging, and Dorn.SharedKernel -- and emits them to ./artifacts, where the
# root nuget.config's "dorn-local" source picks them up as a local package feed.
#
# templates/webapi consumes these three packages via ordinary PackageReference (see
# docs/adr/0011-extract-messaging-and-shared-kernel-as-nuget-packages.md); since none
# of them are published to NuGet.org yet, this script must run before `dotnet restore`
# can resolve them, both locally and in CI.
#
# What this script does:
#   1. Runs `dotnet pack` against each of the 3 packages/*.csproj projects.
#   2. Emits the resulting .nupkg files to ./artifacts and prints their paths.
#
# Usage:
#   pwsh eng/scripts/pack-packages.ps1
#   pwsh eng/scripts/pack-packages.ps1 -Version 1.2.3
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
$artifactsDir = Join-Path $repoRoot "artifacts"

$projects = @(
    "Dorn.Messaging.Contracts",
    "Dorn.Messaging",
    "Dorn.SharedKernel"
)

foreach ($projectName in $projects) {
    $projectPath = Join-Path $repoRoot "packages/$projectName/$projectName.csproj"

    Write-Host "==> Packing $projectPath (version $Version)..."

    dotnet pack $projectPath -c Release "-p:PackageVersion=$Version" -o $artifactsDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet pack failed (exit code $LASTEXITCODE) for $projectPath."
        exit 1
    }

    $nupkg = Get-ChildItem -Path $artifactsDir -Filter "$projectName.$Version.nupkg" -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if (-not $nupkg) {
        Write-Error "dotnet pack reported success but no matching .nupkg was found under $artifactsDir (expected $projectName.$Version.nupkg)."
        exit 1
    }

    Write-Host "==> Packed successfully:"
    Write-Host $nupkg.FullName
}

exit 0
