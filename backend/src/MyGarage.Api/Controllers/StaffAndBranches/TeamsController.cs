using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/teams")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class TeamsController : ControllerBase
    {
        private readonly ITeamService _teamService;

        public TeamsController(ITeamService teamService)
        {
            _teamService = teamService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TeamResponseDto>>> GetAll([FromQuery] long? branchId = null, [FromQuery] string? status = null)
        {
            var items = await _teamService.GetAllAsync();

            if (branchId.HasValue)
            {
                items = items.Where(x => x.BranchId == branchId.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                items = items.Where(x => string.Equals((x.Status ?? string.Empty).Trim(), normalized, System.StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return Ok(_teamService.MapToDtos(items));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<TeamResponseDto>> GetById(long id)
        {
            var dto = await _teamService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<TeamResponseDto>> Create([FromBody] TeamCreateDto itemDto)
        {
            var responseDto = await _teamService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<TeamResponseDto>> Update(long id, [FromBody] TeamUpdateDto itemDto)
        {
            var responseDto = await _teamService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }

            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _teamService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
