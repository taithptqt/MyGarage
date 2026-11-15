param(
    [Parameter(Mandatory=$true)][ValidateSet('Backend','Frontend','CameraAI')][string]$Service,
    [ValidateSet('cpu','cuda')][string]$Backend='cpu'
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
switch($Service) {
    'Backend' { & dotnet run --project (Join-Path $projectRoot 'backend\src\MyGarage.Api') --launch-profile https }
    'Frontend' { Set-Location -LiteralPath (Join-Path $projectRoot 'Frontend'); & npm.cmd run dev -- --host 127.0.0.1 --strictPort }
    'CameraAI' { & (Join-Path $PSScriptRoot 'start-camera-ai.ps1') -Backend $Backend -Port 7861 }
}
exit $LASTEXITCODE
