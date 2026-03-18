$modelsDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Models"
$dtosReqDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\DTOs\Requests"
$dtosResDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\DTOs\Responses"
$mappingsFile = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Mappings\MappingProfile.cs"
$ctrlDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Controllers"

$mappingContent = Get-Content $mappingsFile -Raw

$files = Get-ChildItem -Path $modelsDir -Filter *.cs
foreach ($file in $files) {
    $modelName = $file.BaseName
    if ($modelName -in @("Vehicle", "Customer", "ModelSpecificRule")) { continue }

    $content = Get-Content $file.FullName
    $props = @()
    $allprops = @()

    foreach ($line in $content) {
        if ($line -match '^\s*public\s+([A-Za-z0-9_<>\[\]\?]+)\s+([A-Za-z0-9_]+)\s*\{\s*get;\s*set;\s*\}') {
            $type = $matches[1]
            $name = $matches[2]
            
            if (-not ($type -match "ICollection") -and -not ($line -match "virtual")) {
                $allprops += "        public $type $name { get; set; }"
                if ($name -notin @("Id", "CreatedAt", "UpdatedAt", "PasswordHash", "SecurityStamp")) {
                    $props += "        public $type $name { get; set; }"
                }
            }
        }
    }

    # Generate DTOs
    $createDto = "using System;`nnamespace MyGarage.DTOs.Requests`n{`n    public class $($modelName)CreateDto`n    {`n$($props -join "`r`n")`n    }`n}"
    Set-Content -Path "$dtosReqDir\$($modelName)CreateDto.cs" -Value $createDto

    $updateDto = "using System;`nnamespace MyGarage.DTOs.Requests`n{`n    public class $($modelName)UpdateDto`n    {`n$($props -join "`r`n")`n    }`n}"
    Set-Content -Path "$dtosReqDir\$($modelName)UpdateDto.cs" -Value $updateDto

    $responseDto = "using System;`nnamespace MyGarage.DTOs.Responses`n{`n    public class $($modelName)ResponseDto`n    {`n$($allprops -join "`r`n")`n    }`n}"
    Set-Content -Path "$dtosResDir\$($modelName)ResponseDto.cs" -Value $responseDto

    # Append to MappingProfile
    if (-not $mappingContent.Contains("CreateMap<$modelName, $($modelName)ResponseDto>();")) {
        $mapStr = "            // $($modelName) Mappings`r`n            CreateMap<$modelName, $($modelName)ResponseDto>();`r`n            CreateMap<$($modelName)CreateDto, $modelName>();`r`n            CreateMap<$($modelName)UpdateDto, $modelName>();"
        $mappingContent = $mappingContent -replace "\s*\}\s*\}\s*$", "`r`n$mapStr`r`n        }`r`n    }`r`n"
    }

    # Patch Controller
    $ctrlFile = "$ctrlDir\$($modelName)Controller.cs"
    if (Test-Path $ctrlFile) {
        $cContent = Get-Content $ctrlFile -Raw
        $lowerName = $modelName.Substring(0,1).ToLower() + $modelName.Substring(1)

        if (-not $cContent.Contains("using AutoMapper;")) {
            $cContent = $cContent -replace "using MyGarage.Interfaces;", "using AutoMapper;`r`nusing MyGarage.Interfaces;`r`nusing MyGarage.DTOs.Requests;`r`nusing MyGarage.DTOs.Responses;"
        }

        if (-not $cContent.Contains("IMapper _mapper")) {
            $cContent = $cContent -replace "private readonly I$($modelName)Service _$($lowerName)Service;", "private readonly I$($modelName)Service _$($lowerName)Service;`r`n        private readonly IMapper _mapper;"
            $cContent = $cContent -replace "public $($modelName)Controller\(I$($modelName)Service $($lowerName)Service\)", "public $($modelName)Controller(I$($modelName)Service $($lowerName)Service, IMapper mapper)"
            $cContent = $cContent -replace "_$($lowerName)Service = $($lowerName)Service;", "_$($lowerName)Service = $($lowerName)Service;`r`n            _mapper = mapper;"
        }

        # GetAll
        $cContent = $cContent -replace "public async Task<ActionResult<IEnumerable<$modelName>>> GetAll\(\)", "public async Task<ActionResult<IEnumerable<$($modelName)ResponseDto>>> GetAll()"
        $cContent = $cContent -replace "return Ok\(items\);", "var dtos = _mapper.Map<IEnumerable<$($modelName)ResponseDto>>(items);`r`n            return Ok(dtos);"
        # Fix accidental duplicate Ok(dtos)
        $cContent = $cContent -replace "var dtos = _mapper.Map<IEnumerable<$($modelName)ResponseDto>>\(items\);`r`n            var dtos = _mapper.Map<IEnumerable<$($modelName)ResponseDto>>\(items\);`r`n            return Ok\(dtos\);", "var dtos = _mapper.Map<IEnumerable<$($modelName)ResponseDto>>(items);`r`n            return Ok(dtos);"

        # GetById
        $cContent = $cContent -replace "public async Task<ActionResult<$modelName>> GetById\(long id\)", "public async Task<ActionResult<$($modelName)ResponseDto>> GetById(long id)"
        $cContent = $cContent -replace "return Ok\(item\);", "var dto = _mapper.Map<$($modelName)ResponseDto>(item);`r`n            return Ok(dto);"

        # Create
        $cContent = $cContent -replace "public async Task<ActionResult<$modelName>> Create\(\[FromBody\] $modelName item\)", "public async Task<ActionResult<$($modelName)ResponseDto>> Create([FromBody] $($modelName)CreateDto itemDto)"
        $cContent = $cContent -replace "var createdItem = await _$($lowerName)Service.AddAsync\(item\);", "var entity = _mapper.Map<$modelName>(itemDto);`r`n            var createdItem = await _$($lowerName)Service.AddAsync(entity);"
        $cContent = $cContent -replace "return CreatedAtAction\(nameof\(GetById\), new \{ id = createdItem.Id \}, createdItem\);", "var responseDto = _mapper.Map<$($modelName)ResponseDto>(createdItem);`r`n            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);"

        # Update
        $cContent = $cContent -replace "public async Task<ActionResult<$modelName>> Update\(long id, \[FromBody\] $modelName item\)", "public async Task<ActionResult<$($modelName)ResponseDto>> Update(long id, [FromBody] $($modelName)UpdateDto itemDto)"
        $cContent = $cContent -replace "item.Id = id;`r`n            var updatedItem = await _$($lowerName)Service.UpdateAsync\(id, item\);", "var existingItem = await _$($lowerName)Service.GetByIdAsync(id);`r`n            if (existingItem == null) return NotFound();`r`n`r`n            _mapper.Map(itemDto, existingItem);`r`n            var updatedItem = await _$($lowerName)Service.UpdateAsync(id, existingItem);"
        $cContent = $cContent -replace "return Ok\(updatedItem\);", "var responseDto = _mapper.Map<$($modelName)ResponseDto>(updatedItem);`r`n            return Ok(responseDto);"

        Set-Content -Path $ctrlFile -Value $cContent
    }
}

Set-Content -Path $mappingsFile -Value $mappingContent
Write-Host "Batch DTO generation complete."
