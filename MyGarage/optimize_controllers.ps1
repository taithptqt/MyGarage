$controllersDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Controllers"
Get-ChildItem -Path $controllersDir -Filter *Controller.cs | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    
    # 1. Optimize Create by reverting back to CreatedAtAction
    $oldCreate = "Ok(createdItem)"
    $newCreate = "CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, createdItem)"
    if ($content.Contains($oldCreate)) {
        $content = $content.Replace($oldCreate, $newCreate)
    }

    # 2. Optimize Update by reverting string reflection to strong typing
    $oldUpdate = "item.GetType().GetProperty(`"Id`")?.SetValue(item, id)"
    $newUpdate = "item.Id = id"
    if ($content.Contains($oldUpdate)) {
        $content = $content.Replace($oldUpdate, $newUpdate)
    }

    Set-Content -Path $_.FullName -Value $content
}
Write-Host "Updated all controllers to use CreatedAtAction and direct Id assignment."
