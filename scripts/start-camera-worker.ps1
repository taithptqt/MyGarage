param([Parameter(Mandatory=$true)][string]$Config,
    [ValidateSet('cpu','cuda')][string]$Backend='cpu')
$ErrorActionPreference='Stop'
$cameraRoot=Join-Path (Split-Path $PSScriptRoot -Parent) 'services\camera-ai'
$configPath=(Resolve-Path -LiteralPath $Config).Path
$python=Join-Path $cameraRoot '.venv\Scripts\python.exe'
if(-not (Test-Path -LiteralPath $python)){throw 'Install Camera AI with scripts/start-camera-ai.ps1 -Install first.'}
$previousBackend=$env:CAMERA_AI_BACKEND
try {
    $env:CAMERA_AI_BACKEND=$Backend
    & $python (Join-Path $cameraRoot 'worker.py') --config $configPath
    if($LASTEXITCODE -ne 0){throw 'Camera worker stopped with an error.'}
}
finally {$env:CAMERA_AI_BACKEND=$previousBackend}
