$src = "D:\IT\OJT\repos\MyGarage_V2"
$dest = "D:\IT\OJT\repos\MyGarage"
$excludes = @(".git", ".vs", "obj", "bin", "node_modules", ".next", "dist", "diffs.json", "compare_projects.ps1")

function Get-Files($path) {
    Get-ChildItem -Path $path -Recurse -File | Where-Object { 
        $rel = $_.FullName.Substring($path.Length)
        foreach ($ex in $excludes) {
            if ($rel -like "*\$ex\*" -or $rel -like "$ex\*") { return $false }
        }
        return $true
    }
}

$files2 = Get-Files $src
$diffs = New-Object System.Collections.Generic.List[PSObject]

foreach ($f2 in $files2) {
    $relPath = $f2.FullName.Substring($src.Length + 1)
    $f1Path = Join-Path $dest $relPath
    
    if (-not (Test-Path $f1Path)) {
        $diffs.Add([PSCustomObject]@{ Status = "Missing"; Path = $relPath })
    } else {
        $f1 = Get-Item $f1Path
        if ($f1.Length -ne $f2.Length) {
            $diffs.Add([PSCustomObject]@{ Status = "Different"; Path = $relPath })
        } else {
            $hash1 = (Get-FileHash $f1Path).Hash
            $hash2 = (Get-FileHash $f2.FullName).Hash
            if ($hash1 -ne $hash2) {
                $diffs.Add([PSCustomObject]@{ Status = "Different"; Path = $relPath })
            }
        }
    }
}

$json = $diffs | ConvertTo-Json
[System.IO.File]::WriteAllText("D:\IT\OJT\repos\MyGarage\diffs.json", $json, [System.Text.Encoding]::UTF8)
