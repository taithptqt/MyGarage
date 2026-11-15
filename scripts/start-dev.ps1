param([ValidateSet('cpu','cuda')][string]$Backend='cpu', [switch]$Stop)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$runtimeRoot=Join-Path $projectRoot '.dev-runtime'
$statePath=Join-Path $runtimeRoot 'processes.json'
$runner=Join-Path $PSScriptRoot 'run-dev-service.ps1'
New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
$managed=@()
if(Test-Path -LiteralPath $statePath) { $managed=Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json }
if($Stop) {
    function Stop-DevTree([int]$processId) {
        Get-CimInstance Win32_Process -Filter "ParentProcessId=$processId" | ForEach-Object { Stop-DevTree $_.ProcessId }
        Stop-Process -Id $processId -ErrorAction SilentlyContinue
    }
    foreach($entry in $managed) {
        $process=Get-Process -Id $entry.id -ErrorAction SilentlyContinue
        $info=Get-CimInstance Win32_Process -Filter "ProcessId=$($entry.id)" -ErrorAction SilentlyContinue
        if($process -and $process.StartTime.ToUniversalTime().ToString('o') -eq $entry.started -and $info.CommandLine.Contains($runner)) {
            Stop-DevTree $process.Id
        }
    }
    Remove-Item -LiteralPath $statePath -ErrorAction SilentlyContinue
    Write-Output 'Stopped services started by this launcher. Existing external processes were retained.'
    exit
}
function Test-DevReady($service) {
    try {
        $response=Invoke-WebRequest -Uri $service.url -UseBasicParsing -TimeoutSec 2 -MaximumRedirection 0
        if($service.name -eq 'CameraAI') { return ($response.Content | ConvertFrom-Json).service -eq 'camera-ai' }
        if($service.name -eq 'Backend') { return $response.StatusCode -in @(200,307) }
        return $response.StatusCode -eq 200
    } catch {
        return $service.name -eq 'Backend' -and [int]$_.Exception.Response.StatusCode -eq 401
    }
}
$services=@(
    @{name='Backend';url='http://127.0.0.1:5062/api/v1/gate/cameras';port=5062},
    @{name='Frontend';url='http://127.0.0.1:5173';port=5173},
    @{name='CameraAI';url='http://127.0.0.1:7861/health';port=7861}
)
foreach($service in $services) {
    if(Test-DevReady $service) { Write-Output "$($service.name): already running"; continue }
    if(Get-NetTCPConnection -State Listen -LocalPort $service.port -ErrorAction SilentlyContinue) { throw "Port $($service.port) is occupied by a service that is not ready. Check it before restarting." }
    $arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',('"{0}"' -f $runner),'-Service',$service.name,'-Backend',$Backend)
    $process=Start-Process -FilePath powershell.exe -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $runtimeRoot "$($service.name).out.log") -RedirectStandardError (Join-Path $runtimeRoot "$($service.name).err.log")
    $managed=@($managed)+@(@{id=$process.Id;started=$process.StartTime.ToUniversalTime().ToString('o');service=$service.name})
    ConvertTo-Json -InputObject @($managed) | Set-Content -LiteralPath $statePath -Encoding UTF8
    Write-Output "$($service.name): starting"
}
$deadline=(Get-Date).AddSeconds(90)
do {
    $waiting=@($services | Where-Object { -not (Test-DevReady $_) })
    if(-not $waiting.Count) { break }
    Start-Sleep -Milliseconds 700
} while((Get-Date) -lt $deadline)
if($waiting.Count) { throw "Services not ready: $($waiting.name -join ', '). See $runtimeRoot" }
Write-Output 'Garage:   http://localhost:5173/workshop/gate'
Write-Output 'CameraAI: http://127.0.0.1:7861 (original AI workbench)'
Write-Output 'Backend:  https://localhost:7009 / http://localhost:5062'
Write-Output 'Stop managed services: powershell -File scripts/start-dev.ps1 -Stop'
