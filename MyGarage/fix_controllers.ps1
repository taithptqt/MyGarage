$controllersDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Controllers"
Get-ChildItem -Path $controllersDir -Filter *Controller.cs | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    # Use string replacement to avoid regex issues with characters
    $oldStr = "CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, createdItem)"
    $newStr = "Ok(createdItem)"
    if ($content.Contains($oldStr)) {
        $content = $content.Replace($oldStr, $newStr)
        Set-Content -Path $_.FullName -Value $content
        Write-Host "Updated $($_.Name)"
    }
}
Write-Host "Done fixing controllers."
