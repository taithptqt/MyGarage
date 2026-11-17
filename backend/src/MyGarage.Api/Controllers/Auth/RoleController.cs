using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/roles")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoleResponseDto>>> GetAll()
        {
            var dtos = await _roleService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RoleResponseDto>> GetById(long id)
        {
            var dto = await _roleService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [Authorize(Policy = "AdminStaffOnly")]
        [HttpPost]
        public async Task<ActionResult<RoleResponseDto>> Create([FromBody] RoleCreateDto itemDto)
        {
            var responseDto = await _roleService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [Authorize(Policy = "AdminStaffOnly")]
        [HttpPut("{id}")]
        public async Task<ActionResult<RoleResponseDto>> Update(long id, [FromBody] RoleUpdateDto itemDto)
        {
            var responseDto = await _roleService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [Authorize(Policy = "AdminStaffOnly")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _roleService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






