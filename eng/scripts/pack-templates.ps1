# Pack templates/webapi as a NuGet template package to ./artifacts
param([string]$Version = "0.1.0-dev")
$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$packagingProject = Join-Path $repoRoot "eng/packaging/Dorn.Templates.WebApi/Dorn.Templates.WebApi.csproj"
$artifactsDir = Join-Path $repoRoot "artifacts"

Write-Host "==> Packing $packagingProject (version $Version)..."
dotnet pack $packagingProject -c Release "-p:PackageVersion=$Version" -o $artifactsDir
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet pack failed (exit code $LASTEXITCODE) for $packagingProject."; exit 1 }
$nupkg = Get-ChildItem -Path $artifactsDir -Filter "Dorn.Templates.WebApi.$Version.nupkg" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $nupkg) { Write-Error "No .nupkg found under $artifactsDir (expected Dorn.Templates.WebApi.$Version.nupkg)."; exit 1 }
Write-Host "==> Packed: $($nupkg.FullName)"
exit 0
