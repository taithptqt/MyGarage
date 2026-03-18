$modelsDir = "c:\Users\DELL\source\repos\MyGarage\MyGarage\Models"
$basePath = "c:\Users\DELL\source\repos\MyGarage\MyGarage"
$programCsPath = "$basePath\Program.cs"

$programCsContent = Get-Content $programCsPath -Raw

$diLinesToAdd = @()

Get-ChildItem -Path $modelsDir -Filter *.cs | ForEach-Object {
    $model = $_.BaseName
    
    # 1. IRepository
    $iRepoPath = "$basePath\Interfaces\I$($model)Repository.cs"
    if (-not (Test-Path $iRepoPath)) {
        $iRepoContent = @"
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface I$($model)Repository : IGenericRepository<$model>
    {
    }
}
"@
        Set-Content -Path $iRepoPath -Value $iRepoContent
    }

    # 2. Repository
    $repoPath = "$basePath\Repositories\$($model)Repository.cs"
    if (-not (Test-Path $repoPath)) {
        $repoContent = @"
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class $($model)Repository : GenericRepository<$model>, I$($model)Repository
    {
        public $($model)Repository(GarageContext context) : base(context)
        {
        }
    }
}
"@
        Set-Content -Path $repoPath -Value $repoContent
    }

    # 3. IService
    $iSvcPath = "$basePath\Interfaces\I$($model)Service.cs"
    if (-not (Test-Path $iSvcPath)) {
        $iSvcContent = @"
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface I$($model)Service : IGenericService<$model>
    {
    }
}
"@
        Set-Content -Path $iSvcPath -Value $iSvcContent
    }

    # 4. Service
    $svcPath = "$basePath\Services\$($model)Service.cs"
    if (-not (Test-Path $svcPath)) {
        $svcContent = @"
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class $($model)Service : GenericService<$model>, I$($model)Service
    {
        public $($model)Service(I$($model)Repository repository) : base(repository)
        {
        }
    }
}
"@
        Set-Content -Path $svcPath -Value $svcContent
    }

    # 5. Controller
    $ctrlPath = "$basePath\Controllers\$($model)Controller.cs"
    if (-not (Test-Path $ctrlPath)) {
        $lowerModel = $model.Substring(0,1).ToLower() + $model.Substring(1)
        $ctrlContent = @"
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class $($model)Controller : ControllerBase
    {
        private readonly I$($model)Service _$($lowerModel)Service;

        public $($model)Controller(I$($model)Service $($lowerModel)Service)
        {
            _$($lowerModel)Service = $($lowerModel)Service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<$model>>> GetAll()
        {
            var items = await _$($lowerModel)Service.GetAllAsync();
            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<$model>> GetById(long id)
        {
            var item = await _$($lowerModel)Service.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            return Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<$model>> Create([FromBody] $model item)
        {
            var createdItem = await _$($lowerModel)Service.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, createdItem);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<$model>> Update(long id, [FromBody] $model item)
        {
            item.GetType().GetProperty("Id")?.SetValue(item, id);
            var updatedItem = await _$($lowerModel)Service.UpdateAsync(id, item);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            return Ok(updatedItem);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _$($lowerModel)Service.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}
"@
        Set-Content -Path $ctrlPath -Value $ctrlContent
    }

    $diLine1 = "builder.Services.AddScoped<I$($model)Repository, $($model)Repository>();"
    $diLine2 = "builder.Services.AddScoped<I$($model)Service, $($model)Service>();"
    
    if (-not $programCsContent.Contains($diLine1)) {
        $diLinesToAdd += $diLine1
        $diLinesToAdd += $diLine2
        $diLinesToAdd += ""
    }
}

if ($diLinesToAdd.Count -gt 0) {
    # Fix the missing newline before we append the comment back
    $diBlock = $diLinesToAdd -join "`r`n"
    $programCsContent = $programCsContent -replace "// Add services to the container.", "$diBlock`n// Add services to the container."
    Set-Content -Path $programCsPath -Value $programCsContent
}

Write-Host "Scaffolding completed successfully."
