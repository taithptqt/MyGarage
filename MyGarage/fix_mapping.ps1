$mappingsFile = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Mappings\MappingProfile.cs"
$modelsDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Models"
$files = Get-ChildItem -Path $modelsDir -Filter *.cs

$str = "using AutoMapper;`r`nusing MyGarage.Models;`r`nusing MyGarage.DTOs.Requests;`r`nusing MyGarage.DTOs.Responses;`r`n`r`nnamespace MyGarage.Mappings`r`n{`r`n    public class MappingProfile : Profile`r`n    {`r`n        public MappingProfile()`r`n        {"

foreach ($file in $files) {
    $modelName = $file.BaseName
    if ($modelName -eq "ModelSpecificRule") { continue }
    
    $str += "`r`n            // $($modelName) Mappings"
    $str += "`r`n            CreateMap<$modelName, $($modelName)ResponseDto>();"
    $str += "`r`n            CreateMap<$($modelName)CreateDto, $modelName>();"
    $str += "`r`n            CreateMap<$($modelName)UpdateDto, $modelName>();"
}

$str += "`r`n        }`r`n    }`r`n}"
Set-Content -Path $mappingsFile -Value $str
Write-Host "MappingProfile rebuilt."
