using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/staff-schedules")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class StaffScheduleController : ControllerBase
    {
        private readonly IStaffScheduleService _staffScheduleService;

        public StaffScheduleController(IStaffScheduleService staffScheduleService)
        {
            _staffScheduleService = staffScheduleService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StaffScheduleResponseDto>>> GetAll()
        {
            var dtos = await _staffScheduleService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StaffScheduleResponseDto>> GetById(long id)
        {
            var dto = await _staffScheduleService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StaffScheduleResponseDto>> Create([FromBody] StaffScheduleCreateDto itemDto)
        {
            var responseDto = await _staffScheduleService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<StaffScheduleResponseDto>> Update(long id, [FromBody] StaffScheduleUpdateDto itemDto)
        {
            var responseDto = await _staffScheduleService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _staffScheduleService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






