# Pack Dorn packages to ./artifacts for local dev/CI feed
param([string]$Version = "1.0.1", [string]$WebUIPrimitivesVersion = $Version)
$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$artifactsDir = Join-Path $repoRoot "artifacts"
$projects = [ordered]@{
    "Dorn.Messaging.Contracts" = $Version
    "Dorn.Messaging"           = $Version
    "Dorn.SharedKernel"        = $Version
    "Dorn.WebUI.Primitives"    = $WebUIPrimitivesVersion
}

foreach ($projectName in $projects.Keys) {
    $packVersion = $projects[$projectName]
    $projectPath = Join-Path $repoRoot "packages/$projectName/$projectName.csproj"
    Write-Host "==> Packing $projectPath (version $packVersion)..."
    dotnet pack $projectPath -c Release "-p:PackageVersion=$packVersion" -o $artifactsDir
    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet pack failed (exit code $LASTEXITCODE) for $projectPath."; exit 1 }
    $nupkg = Get-ChildItem -Path $artifactsDir -Filter "$projectName.$packVersion.nupkg" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $nupkg) { Write-Error "No .nupkg found under $artifactsDir (expected $projectName.$packVersion.nupkg)."; exit 1 }
    Write-Host "==> Packed: $($nupkg.FullName)"
}
exit 0
