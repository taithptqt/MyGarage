param(
    [string]$PythonExe,
    [ValidateSet('cpu', 'cuda')][string]$Backend = 'cpu',
    [ValidateRange(1024, 65535)][int]$Port = 7861,
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$cameraRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'services\camera-ai'
$venvPython = Join-Path $cameraRoot '.venv\Scripts\python.exe'

Push-Location $cameraRoot
try {
    if ($Install) {
        if (-not (Test-Path -LiteralPath $venvPython)) {
            if (-not $PythonExe) { $PythonExe = 'python' }
            & $PythonExe -m venv .venv
            if ($LASTEXITCODE -ne 0) { throw 'Could not create Camera AI virtual environment.' }
        }
        $extra = if ($Backend -eq 'cuda') { 'onnx-gpu' } else { 'onnx' }
        & $venvPython -m pip install -e ".[$extra]"
        if ($LASTEXITCODE -ne 0) { throw 'Could not install Camera AI dependencies.' }
    }
    if (-not (Test-Path -LiteralPath $venvPython)) {
        throw 'Run scripts/start-camera-ai.ps1 -Install -PythonExe <python-3.12-path> first.'
    }
    $previousBackend = $env:CAMERA_AI_BACKEND
    try {
        $env:CAMERA_AI_BACKEND = $Backend
        & $venvPython web_app.py --port $Port
        if ($LASTEXITCODE -ne 0) { throw 'Camera AI stopped with an error.' }
    }
    finally { $env:CAMERA_AI_BACKEND = $previousBackend }
}
finally { Pop-Location }
