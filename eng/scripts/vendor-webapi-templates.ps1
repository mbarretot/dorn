# Vendors the externally-published webapi template pack into templates/webapi (ADR 0028).
param([string]$VendorTarget = "")
$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$pinsFile = Join-Path $repoRoot "Directory.Packages.props"
$webApiPin = (Select-String -Path $pinsFile -Pattern 'Dorn\.Templates\.WebApi" Version="([^"]+)"').Matches[0].Groups[1].Value
$proj = Join-Path $repoRoot "eng/vendoring/Dorn.Templates.WebApi.Vendor/Dorn.Templates.WebApi.Vendor.csproj"
# nuget.config's dorn-local source points at ./artifacts; NuGet validates every configured local
# source exists whenever it needs to actually search (i.e. on any cache miss), so a fresh clone/CI
# checkout without that folder yet fails restore with NU1301 before it ever reaches nuget.org.
New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot "artifacts") | Out-Null
$args = @("-p:WebApiPin=$webApiPin")
if ($VendorTarget) { $args += "-p:VendorTarget=$VendorTarget" }
Write-Host "==> Vendoring Dorn.Templates.WebApi $webApiPin..."
dotnet restore $proj @args
if ($LASTEXITCODE -ne 0) { Write-Error "Restore failed for $proj (pinned webapi template pack unavailable?)."; exit 1 }
dotnet build $proj --no-restore -t:VendorWebApiTemplates @args
if ($LASTEXITCODE -ne 0) { Write-Error "Vendoring target failed."; exit 1 }
exit 0
