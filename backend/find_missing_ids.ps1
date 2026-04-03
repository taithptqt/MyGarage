$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$modelsDir = Join-Path $scriptDir "Models"

if (-not (Test-Path $modelsDir)) {
    Write-Error "Models directory not found: $modelsDir"
    exit 1
}

$files = Get-ChildItem -Path $modelsDir -Filter *.cs
foreach ($file in $files) {
    if (-not (Select-String -Path $file.FullName -Pattern 'public (long|int)\?? Id\s*\{' -Quiet)) {
        Write-Host "No Id in: $($file.Name)"
    }
}
