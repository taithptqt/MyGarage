$controllersDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Controllers"
$files = Get-ChildItem -Path $controllersDir -Filter *.cs

foreach ($file in $files) {
    if ($file.BaseName -in @("VehicleController", "CustomerController", "ModelSpecificRuleController")) { continue }
    
    $cContent = Get-Content $file.FullName -Raw
    $modelName = $file.BaseName.Replace("Controller", "")
    $lowerName = $modelName.Substring(0,1).ToLower() + $modelName.Substring(1)

    $pattern = "(?s)item\.Id\s*=\s*id;\s*var\s+updatedItem\s*=\s*await\s+_$($lowerName)Service\.UpdateAsync\(id,\s*item\);"
    $replacement = "var existingItem = await _$($lowerName)Service.GetByIdAsync(id);`r`n            if (existingItem == null)`r`n            {`r`n                return NotFound();`r`n            }`r`n`r`n            _mapper.Map(itemDto, existingItem);`r`n            var updatedItem = await _$($lowerName)Service.UpdateAsync(id, existingItem);"
    
    if ($cContent -match $pattern) {
        $cContent = $cContent -replace $pattern, $replacement
        Set-Content -Path $file.FullName -Value $cContent
    }
}
Write-Host "Controller patches complete."
