# Pack Dorn packages to ./artifacts for local dev/CI feed
param([string]$Version = "0.1.0-dev")
$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$artifactsDir = Join-Path $repoRoot "artifacts"
$projects = @("Dorn.Messaging.Contracts", "Dorn.Messaging", "Dorn.SharedKernel")

foreach ($projectName in $projects) {
    $projectPath = Join-Path $repoRoot "packages/$projectName/$projectName.csproj"
    Write-Host "==> Packing $projectPath (version $Version)..."
    dotnet pack $projectPath -c Release "-p:PackageVersion=$Version" -o $artifactsDir
    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet pack failed (exit code $LASTEXITCODE) for $projectPath."; exit 1 }
    $nupkg = Get-ChildItem -Path $artifactsDir -Filter "$projectName.$Version.nupkg" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $nupkg) { Write-Error "No .nupkg found under $artifactsDir (expected $projectName.$Version.nupkg)."; exit 1 }
    Write-Host "==> Packed: $($nupkg.FullName)"
}
exit 0
