using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.Exceptions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [ApiController]
    [Route("api/bay-management")]
    [Authorize]
    public class BayManagementController : ControllerBase
    {
        private readonly IBayManagementService _bayManagementService;

        public BayManagementController(IBayManagementService bayManagementService)
        {
            _bayManagementService = bayManagementService;
        }

        /// <summary>
        /// Allocates a bay to a service order and vehicle.
        /// </summary>
        [HttpPost("{id}/allocate")]
        public async Task<IActionResult> AllocateBay(long id, [FromBody] BayAllocationCreateDto dto)
        {
            if (id != dto.BayId)
            {
                return BadRequest(new { Message = "Bay ID in URL does not match DTO" });
            }

            var staffId = this.GetCurrentUserId();
            if (!staffId.HasValue || staffId.Value <= 0)
            {
                return Unauthorized(new { message = "Không xác định được nhân sự thao tác." });
            }

            try
            {
                var result = await _bayManagementService.AllocateBayAsync(dto, staffId.Value);
                return Ok(result);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        /// <summary>
        /// Releases a bay from its current allocation.
        /// </summary>
        [HttpDelete("{id}/release")]
        public async Task<IActionResult> ReleaseBay(long id, [FromQuery] string? notes)
        {
            var staffId = this.GetCurrentUserId();
            if (!staffId.HasValue || staffId.Value <= 0)
            {
                return Unauthorized(new { message = "Không xác định được nhân sự thao tác." });
            }

            try
            {
                var result = await _bayManagementService.ReleaseBayAsync(id, staffId.Value, notes);
                return Ok(result);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { ex.Message });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        /// <summary>
        /// Gets the timeline of bay allocations for a specific date.
        /// </summary>
        [HttpGet("timeline")]
        public async Task<IActionResult> GetTimeline([FromQuery] DateTime? date, [FromQuery] long? branchId)
        {
            var targetDate = date ?? DateTime.UtcNow;
            var targetBranchId = branchId ?? this.GetCurrentBranchId();
            if (!targetBranchId.HasValue || targetBranchId.Value <= 0)
            {
                return BadRequest(new { message = "Thiếu branchId hợp lệ để tải timeline khoang." });
            }

            var result = await _bayManagementService.GetBayTimelineAsync(targetDate, targetBranchId.Value);
            return Ok(result);
        }
    }
}
