using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Authorize(Roles = "Staff")]
    [Route("api/v1/revenue-snapshots")]
    [ApiController]
    public class RevenueSnapshotController : ControllerBase
    {
        private readonly IRevenueSnapshotService _revenueSnapshotService;

        public RevenueSnapshotController(IRevenueSnapshotService revenueSnapshotService)
        {
            _revenueSnapshotService = revenueSnapshotService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RevenueSnapshotResponseDto>>> GetAll()
        {
            var dtos = await _revenueSnapshotService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RevenueSnapshotResponseDto>> GetById(long id)
        {
            var dto = await _revenueSnapshotService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RevenueSnapshotResponseDto>> Create([FromBody] RevenueSnapshotCreateDto itemDto)
        {
            var responseDto = await _revenueSnapshotService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RevenueSnapshotResponseDto>> Update(long id, [FromBody] RevenueSnapshotUpdateDto itemDto)
        {
            var responseDto = await _revenueSnapshotService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _revenueSnapshotService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






