param([switch]$UseLocalCache)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$checksProject = Join-Path $projectRoot 'tests\DoanTrungHieu_2380600643.Checks\DoanTrungHieu_2380600643.Checks.csproj'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if ($UseLocalCache) {
    $nugetCache = Join-Path $env:USERPROFILE '.nuget\packages'
    & dotnet restore $checksProject --source $nugetCache -p:NuGetAudit=false
} else { & dotnet restore $checksProject }
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet build $checksProject --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet run --project $checksProject --no-build
exit $LASTEXITCODE
