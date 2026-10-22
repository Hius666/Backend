param([switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot 'src\DoanTrungHieu_2380600643.Api\DoanTrungHieu_2380600643.Api.csproj'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if ($NoRestore) { & dotnet run --project $apiProject --no-restore }
else { & dotnet run --project $apiProject }
exit $LASTEXITCODE
