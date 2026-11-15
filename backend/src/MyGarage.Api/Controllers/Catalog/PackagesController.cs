using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/packages")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class PackagesController : ControllerBase
    {
        private readonly IPackageService _packageService;

        public PackagesController(IPackageService packageService)
        {
            _packageService = packageService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PackageResponseDto>>> GetAll()
        {
            var items = await _packageService.GetAllResponseAsync();
            return Ok(items);
        }

        [AllowAnonymous]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<PackageResponseDto>> GetById(long id)
        {
            var item = await _packageService.GetByIdResponseAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<PackageResponseDto>> Create([FromBody] PackageCreateDto dto)
        {
            var created = await _packageService.CreateFromDtoAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<PackageResponseDto>> Update(long id, [FromBody] PackageUpdateDto dto)
        {
            var updated = await _packageService.UpdateFromDtoAsync(id, dto);
            return updated == null ? NotFound() : Ok(updated);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var deleted = await _packageService.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }
    }
}
