[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ArtifactsDirectory
)

$ErrorActionPreference = 'Stop'

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Command,
        [Parameter(Mandatory)]
        [string]$Description
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

$artifactsPath = (Resolve-Path -LiteralPath $ArtifactsDirectory).Path
$cliPackage = Get-ChildItem -LiteralPath $artifactsPath -Filter 'Dorn.Cli.*.nupkg' -File |
    Where-Object { $_.Name -notmatch '\.symbols\.nupkg$' } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $cliPackage) {
    throw "Could not find a Dorn.Cli NuGet package in '$artifactsPath'."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$packageArchive = [System.IO.Compression.ZipFile]::OpenRead($cliPackage.FullName)
try {
    $nuspecEntry = $packageArchive.Entries | Where-Object { $_.FullName -match '\.nuspec$' } | Select-Object -First 1
    if ($null -eq $nuspecEntry) {
        throw "Could not find a .nuspec file in '$($cliPackage.Name)'."
    }

    $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
    try {
        [xml]$nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
}
finally {
    $packageArchive.Dispose()
}

$packageVersion = $nuspec.package.metadata.version
if ([string]::IsNullOrWhiteSpace($packageVersion)) {
    throw "Could not determine the version of '$($cliPackage.Name)'."
}

$toolPath = Join-Path ([System.IO.Path]::GetTempPath()) "dorn-cli-smoke-tool-$([guid]::NewGuid().ToString('N'))"
$projectPath = Join-Path ([System.IO.Path]::GetTempPath()) "dorn-cli-smoke-project-$([guid]::NewGuid().ToString('N'))"
$nugetPackagesPath = Join-Path ([System.IO.Path]::GetTempPath()) "dorn-cli-smoke-nuget-packages-$([guid]::NewGuid().ToString('N'))"
$nugetHttpCachePath = Join-Path ([System.IO.Path]::GetTempPath()) "dorn-cli-smoke-nuget-http-cache-$([guid]::NewGuid().ToString('N'))"
$nugetConfigPath = Join-Path ([System.IO.Path]::GetTempPath()) "dorn-cli-smoke-nuget-$([guid]::NewGuid().ToString('N')).config"

$escapedArtifactsPath = [System.Security.SecurityElement]::Escape($artifactsPath)
$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-artifacts" value="$escapedArtifactsPath" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local-artifacts">
      <package pattern="Dorn.Cli" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@

try {
    $nugetConfig | Set-Content -LiteralPath $nugetConfigPath -Encoding utf8
    New-Item -ItemType Directory -Path $toolPath | Out-Null
    New-Item -ItemType Directory -Path $nugetPackagesPath | Out-Null
    New-Item -ItemType Directory -Path $nugetHttpCachePath | Out-Null

    $previousNugetPackagesPath = $env:NUGET_PACKAGES
    $previousNugetHttpCachePath = $env:NUGET_HTTP_CACHE_PATH
    try {
        $env:NUGET_PACKAGES = $nugetPackagesPath
        $env:NUGET_HTTP_CACHE_PATH = $nugetHttpCachePath

        Invoke-NativeCommand -Description 'Installing packed Dorn.Cli tool' -Command {
            dotnet tool install Dorn.Cli --tool-path $toolPath --version $packageVersion --configfile $nugetConfigPath
        }
    }
    finally {
        if ($null -eq $previousNugetPackagesPath) {
            Remove-Item Env:NUGET_PACKAGES -ErrorAction SilentlyContinue
        }
        else {
            $env:NUGET_PACKAGES = $previousNugetPackagesPath
        }

        if ($null -eq $previousNugetHttpCachePath) {
            Remove-Item Env:NUGET_HTTP_CACHE_PATH -ErrorAction SilentlyContinue
        }
        else {
            $env:NUGET_HTTP_CACHE_PATH = $previousNugetHttpCachePath
        }
    }

    $dornExecutable = Join-Path $toolPath 'dorn'
    Invoke-NativeCommand -Description 'Running dorn --help' -Command {
        & $dornExecutable --help
    }

    Invoke-NativeCommand -Description 'Generating a project with the packed dorn tool' -Command {
        & $dornExecutable new webapi PackedCliSmokeTest --output $projectPath --orm efcore --database sqlite --orchestrator docker-compose
    }

    # Verify the generated project ships a local tool manifest pinning Dorn.Cli — this is
    # what makes `dotnet dorn <verb>` work from inside a generated project.
    $manifestPath = Join-Path $projectPath '.config/dotnet-tools.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "Generated project is missing local tool manifest at '$manifestPath'."
    }
    $manifestJson = Get-Content -LiteralPath $manifestPath -Raw
    if ($manifestJson -notmatch '"dorn\.cli"') {
        throw "Generated project's local tool manifest does not pin 'dorn.cli'."
    }

    Invoke-NativeCommand -Description 'Restoring local tools in the generated project' -Command {
        # dotnet tool restore walks up from CWD looking for a manifest, so cd into the
        # generated project first.
        Push-Location -LiteralPath $projectPath
        try {
            dotnet tool restore "--configfile" $nugetConfigPath
        }
        finally {
            Pop-Location
        }
    }

    # Verify `dotnet dorn` resolves via local tool manifest (independent of PATH).
    $dotnetDorn = Join-Path $projectPath 'dotnet-dorn-smoke.ps1'
    @"
Set-Location -LiteralPath '$projectPath'
& dotnet dorn --help | Out-Host
"@ | Set-Content -LiteralPath $dotnetDorn -Encoding utf8
    Invoke-NativeCommand -Description 'Running `dotnet dorn --help` from inside generated project (local-tool resolution)' -Command {
        & pwsh -NoLogo -NoProfile -File $dotnetDorn
    }
    Remove-Item -LiteralPath $dotnetDorn -Force -ErrorAction SilentlyContinue

    Invoke-NativeCommand -Description 'Restoring the generated project' -Command {
        dotnet restore $projectPath "-p:RestoreAdditionalProjectSources=$artifactsPath"
    }

    Invoke-NativeCommand -Description 'Building the generated project' -Command {
        dotnet build $projectPath --configuration Release --no-restore
    }

    # Smoke-test the three new top-level verbs (dorn test / run / coverage). Each emits
    # usage to stderr/stdout; we only assert that --help succeeds here — real end-to-end
    # execution is covered by tests/Templates.Tests against a generated project.
    foreach ($verb in @('test', 'run', 'coverage')) {
        Invoke-NativeCommand -Description "Running `dorn $verb --help` against generated project" -Command {
            & $dornExecutable $verb --help --project $projectPath | Out-Host
        }
    }
}
finally {
    Remove-Item -LiteralPath $toolPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $projectPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $nugetPackagesPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $nugetHttpCachePath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $nugetConfigPath -Force -ErrorAction SilentlyContinue
}
