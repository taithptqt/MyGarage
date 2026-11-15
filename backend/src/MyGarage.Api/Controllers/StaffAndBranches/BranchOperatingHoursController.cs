using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/branch-operating-hours")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class BranchOperatingHoursController : ControllerBase
    {
        private readonly IBranchOperatingHourService _branchOperatingHourService;

        public BranchOperatingHoursController(IBranchOperatingHourService branchOperatingHourService)
        {
            _branchOperatingHourService = branchOperatingHourService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BranchOperatingHourResponseDto>>> GetAll()
        {
            var dtos = await _branchOperatingHourService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [AllowAnonymous]
        [HttpGet("{id:long}")]
        public async Task<ActionResult<BranchOperatingHourResponseDto>> GetById(long id)
        {
            var dto = await _branchOperatingHourService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<BranchOperatingHourResponseDto>> Create([FromBody] BranchOperatingHourCreateDto itemDto)
        {
            var responseDto = await _branchOperatingHourService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<BranchOperatingHourResponseDto>> Update(long id, [FromBody] BranchOperatingHourUpdateDto itemDto)
        {
            var responseDto = await _branchOperatingHourService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }

            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _branchOperatingHourService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
