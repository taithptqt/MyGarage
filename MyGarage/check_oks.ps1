$controllersDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Controllers"
Get-ChildItem -Path $controllersDir -Filter *Controller.cs | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    
    # 1. Optimize GetAll just in case
    $oldGetAll = "return Ok(); // items"
    $newGetAll = "return Ok(items);"
    if ($content.Contains($oldGetAll)) {
        $content = $content.Replace($oldGetAll, $newGetAll)
    }

    Set-Content -Path $_.FullName -Value $content
}
Write-Host "Re-checked Ok() usage across controllers."
