# Vendors the externally-published blazor template packs into templates/blazor/{wasm,server} (ADR 0027).
param([string]$VendorTarget = "")
$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$pinsFile = Join-Path $repoRoot "Directory.Packages.props"
$wasmPin   = (Select-String -Path $pinsFile -Pattern 'Dorn\.Templates\.BlazorWasm" Version="([^"]+)"').Matches[0].Groups[1].Value
$serverPin = (Select-String -Path $pinsFile -Pattern 'Dorn\.Templates\.BlazorServer" Version="([^"]+)"').Matches[0].Groups[1].Value
$proj = Join-Path $repoRoot "eng/vendoring/Dorn.Templates.Blazor.Vendor/Dorn.Templates.Blazor.Vendor.csproj"
$args = @("-p:BlazorWasmPin=$wasmPin", "-p:BlazorServerPin=$serverPin")
if ($VendorTarget) { $args += "-p:VendorTarget=$VendorTarget" }
Write-Host "==> Vendoring Dorn.Templates.BlazorWasm $wasmPin / BlazorServer $serverPin..."
dotnet restore $proj @args
if ($LASTEXITCODE -ne 0) { Write-Error "Restore failed for $proj (pinned blazor template packs unavailable?)."; exit 1 }
dotnet build $proj --no-restore -t:VendorBlazorTemplates @args
if ($LASTEXITCODE -ne 0) { Write-Error "Vendoring target failed."; exit 1 }
exit 0
