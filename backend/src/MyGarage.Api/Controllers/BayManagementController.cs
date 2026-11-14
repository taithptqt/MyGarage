using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Authorization;
using MyGarage.Application.Bays;
using MyGarage.Application.Bays.DTOs;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.Exceptions;

namespace MyGarage.Controllers
{
    [ApiController]
    [Route("api/bay-management")]
    [Route("api/v1/bay-management")]
    [Authorize]
    public class BayManagementController : ControllerBase
    {
        private readonly IBayApplicationService _bayAppService;
        private readonly IBayAuthorizationService _authService;

        public BayManagementController(
            IBayApplicationService bayAppService,
            IBayAuthorizationService authService)
        {
            _bayAppService = bayAppService;
            _authService = authService;
        }

        /// <summary>
        /// Allocates a bay to a service order and vehicle.
        /// </summary>
        [HttpPost("{id:long}/allocate")]
        [HttpPost("/api/bays/{id:long}/allocate")]
        [HttpPost("/api/v1/bays/{id:long}/allocate")]
        public async Task<IActionResult> AllocateBay(long id, [FromBody] BayAllocationCreateDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { message = "Dữ liệu phân bổ khoang không hợp lệ." });
            }

            if (dto.BayId <= 0)
            {
                dto.BayId = id;
            }
            else if (id != dto.BayId)
            {
                return BadRequest(new { Message = "Bay ID in URL does not match DTO" });
            }

            var actor = this.ToUserActorContext();
            if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
            {
                return Unauthorized(new { message = "Không xác định được nhân sự thao tác." });
            }

            if (!await _authService.CanAllocateBayAsync(id, dto.ServiceOrderId, actor))
            {
                return Forbid();
            }

            try
            {
                var result = await _bayAppService.AllocateBayAsync(new BayAllocationInput(
                    id,
                    dto.ServiceOrderId,
                    dto.VehicleId,
                    dto.PlannedDuration,
                    dto.Notes
                ), actor);

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
        [HttpDelete("{id:long}/release")]
        [HttpDelete("/api/bays/{id:long}/release")]
        [HttpDelete("/api/v1/bays/{id:long}/release")]
        public async Task<IActionResult> ReleaseBay(long id, [FromQuery] string? notes)
        {
            var actor = this.ToUserActorContext();
            if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
            {
                return Unauthorized(new { message = "Không xác định được nhân sự thao tác." });
            }

            if (!await _authService.CanReleaseBayAsync(id, actor))
            {
                return Forbid();
            }

            try
            {
                var result = await _bayAppService.ReleaseBayAsync(id, notes, actor);
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
        [HttpGet("/api/bays/timeline")]
        [HttpGet("/api/v1/bays/timeline")]
        public async Task<IActionResult> GetTimeline([FromQuery] DateTime? date, [FromQuery] long? branchId)
        {
            if (!this.TryResolveRequiredBranchId(branchId, out var targetBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            var targetDate = date ?? VietnamTime.Now;
            var result = await _bayAppService.GetBayTimelineAsync(targetDate, targetBranchId, actor);
            return Ok(result);
        }
    }
}
