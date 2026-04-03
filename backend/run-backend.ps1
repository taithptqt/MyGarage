# Script to clean up port 7009 and start the backend
$Port = 7009
$ProcessId = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -First 1

if ($ProcessId) {
    Write-Host "Found process $ProcessId on port $Port. Killing it..." -ForegroundColor Yellow
    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

Write-Host "Starting Backend with dotnet run..." -ForegroundColor Green
dotnet run
