using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/teams")]
    [Route("api/v1/teams")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class TeamsController : ControllerBase
    {
        private readonly ITeamService _teamService;
        private readonly IMapper _mapper;

        public TeamsController(ITeamService teamService, IMapper mapper)
        {
            _teamService = teamService;
            _mapper = mapper;
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

            return Ok(_mapper.Map<IEnumerable<TeamResponseDto>>(items));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<TeamResponseDto>> GetById(long id)
        {
            var item = await _teamService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<TeamResponseDto>(item));
        }

        [HttpPost]
        public async Task<ActionResult<TeamResponseDto>> Create([FromBody] TeamCreateDto itemDto)
        {
            var entity = _mapper.Map<Team>(itemDto);
            var createdItem = await _teamService.AddAsync(entity);
            var responseDto = _mapper.Map<TeamResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<TeamResponseDto>> Update(long id, [FromBody] TeamUpdateDto itemDto)
        {
            var existingItem = await _teamService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _teamService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<TeamResponseDto>(updatedItem));
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
