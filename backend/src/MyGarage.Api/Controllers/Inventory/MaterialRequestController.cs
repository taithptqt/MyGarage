using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/material-requests")]
    [ApiController]
    [Authorize]
    public class MaterialRequestController : ControllerBase
    {
        public sealed record MaterialRequestIssueEndpointDto(
            long? IssuedBy,
            long? ActorStaffId,
            string? Note,
            List<MaterialRequestIssuanceItemDto>? Items);

        public sealed record MaterialRequestRejectEndpointDto(
            string? Reason,
            string? Note,
            long? RejectedBy,
            long? ActorStaffId);

        private readonly IMaterialRequestService _materialRequestService;

        public MaterialRequestController(IMaterialRequestService materialRequestService)
        {
            _materialRequestService = materialRequestService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var envelope = await _materialRequestService.GetSummaryPageAsync(
                scopedBranchId,
                status,
                page,
                pageSize,
                HttpContext.RequestAborted);

            return Ok(envelope);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var result = await _materialRequestService.GetScopedSummaryByIdAsync(
                id,
                scopedBranchId,
                HttpContext.RequestAborted);

            return MapCommandResult(result);
        }

        [HttpPost]
        public async Task<ActionResult<MaterialRequestResponseDto>> Create([FromBody] MaterialRequestCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var result = await _materialRequestService.CreateScopedAsync(
                itemDto,
                scopedBranchId,
                this.GetCurrentUserId(),
                HttpContext.RequestAborted);

            switch (result.Status)
            {
                case MaterialRequestCommandStatus.Forbidden:
                    return Forbid();
                case MaterialRequestCommandStatus.NotFound:
                    return NotFound();
                case MaterialRequestCommandStatus.BadRequest:
                    return this.BadRequestProblem(result.ErrorMessage ?? "Invalid request.");
                default:
                    return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data!);
            }
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<MaterialRequestResponseDto>> Update(long id, [FromBody] MaterialRequestUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var result = await _materialRequestService.UpdateScopedAsync(
                id,
                itemDto,
                scopedBranchId,
                HttpContext.RequestAborted);

            switch (result.Status)
            {
                case MaterialRequestCommandStatus.Forbidden:
                    return Forbid();
                case MaterialRequestCommandStatus.NotFound:
                    return NotFound();
                case MaterialRequestCommandStatus.BadRequest:
                    return this.BadRequestProblem(result.ErrorMessage ?? "Invalid request.");
                default:
                    return Ok(result.Data!);
            }
        }

        [HttpPost("{id:long}/issuances")]
        public async Task<IActionResult> Issue(long id, [FromBody] MaterialRequestIssueEndpointDto? payload)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "WAREHOUSE", "STOREKEEPER", "INVENTORY", "THU_KHO", "FOREMAN", "TEAM_LEAD", "ADVISOR"))
            {
                return Forbid();
            }

            var result = await _materialRequestService.IssueScopedAsync(
                id,
                scopedBranchId,
                this.GetCurrentUserId(),
                payload?.ActorStaffId ?? payload?.IssuedBy,
                payload?.Note,
                payload?.Items,
                HttpContext.RequestAborted);

            return MapCommandResult(result);
        }

        [HttpPost("{id:long}/rejections")]
        public async Task<IActionResult> Reject(long id, [FromBody] MaterialRequestRejectEndpointDto? payload)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "WAREHOUSE", "STOREKEEPER", "INVENTORY", "THU_KHO", "FOREMAN", "TEAM_LEAD", "ADVISOR"))
            {
                return Forbid();
            }

            var result = await _materialRequestService.RejectScopedAsync(
                id,
                scopedBranchId,
                this.GetCurrentUserId(),
                payload?.ActorStaffId ?? payload?.RejectedBy,
                payload?.Reason ?? payload?.Note,
                HttpContext.RequestAborted);

            return MapCommandResult(result);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var result = await _materialRequestService.DeleteScopedAsync(
                id,
                scopedBranchId,
                HttpContext.RequestAborted);

            return result.Status switch
            {
                MaterialRequestCommandStatus.NotFound => NotFound(),
                MaterialRequestCommandStatus.Forbidden => Forbid(),
                MaterialRequestCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Invalid request."),
                _ => NoContent()
            };
        }

        private IActionResult MapCommandResult<T>(MaterialRequestCommandResult<T> result)
        {
            return result.Status switch
            {
                MaterialRequestCommandStatus.NotFound => !string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? this.NotFoundProblem(result.ErrorMessage)
                    : NotFound(),
                MaterialRequestCommandStatus.Forbidden => Forbid(),
                MaterialRequestCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Invalid request."),
                _ => Ok(result.Data!)
            };
        }
    }
}
