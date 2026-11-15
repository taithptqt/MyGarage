using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/staff-tiers")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class StaffTierController : ControllerBase
    {
        private readonly IStaffTierService _staffTierService;

        public StaffTierController(IStaffTierService staffTierService)
        {
            _staffTierService = staffTierService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StaffTierResponseDto>>> GetAll()
        {
            var dtos = await _staffTierService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StaffTierResponseDto>> GetById(long id)
        {
            var dto = await _staffTierService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StaffTierResponseDto>> Create([FromBody] StaffTierCreateDto itemDto)
        {
            var responseDto = await _staffTierService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<StaffTierResponseDto>> Update(long id, [FromBody] StaffTierUpdateDto itemDto)
        {
            var responseDto = await _staffTierService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _staffTierService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






