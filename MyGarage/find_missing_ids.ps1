$modelsDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Models"
$files = Get-ChildItem -Path $modelsDir -Filter *.cs
foreach ($file in $files) {
    if (-not (Select-String -Path $file.FullName -Pattern 'public (long|int)\?? Id\s*\{' -Quiet)) {
        Write-Host "No Id in: $($file.Name)"
    }
}
