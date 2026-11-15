using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Exceptions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [ApiController]
    [Route("api/v1/timesheets")]
    [Authorize]
    public sealed class TimesheetsController : ControllerBase
    {
        private readonly ITimesheetService _timesheetService;
        private readonly ITimesheetQueryService _timesheetQueryService;

        public TimesheetsController(ITimesheetService timesheetService, ITimesheetQueryService timesheetQueryService)
        {
            _timesheetService = timesheetService;
            _timesheetQueryService = timesheetQueryService;
        }

        [HttpGet("today")]
        public async Task<ActionResult<IReadOnlyList<TimesheetEntryDto>>> GetToday([FromQuery] long? staffId = null)
        {
            var resolvedStaffId = await ResolveAccessibleStaffIdAsync(staffId);
            if (!resolvedStaffId.HasValue)
            {
                return Forbid();
            }

            try
            {
                var data = await _timesheetService.GetTodayEntriesAsync(resolvedStaffId.Value);
                return Ok(data);
            }
            catch (NotFoundException ex)
            {
                return this.NotFoundProblem(ex.Message);
            }
            catch (BadRequestException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] long? staffId = null)
        {
            var resolvedStaffId = await ResolveAccessibleStaffIdAsync(staffId);
            if (!resolvedStaffId.HasValue)
            {
                return Forbid();
            }

            try
            {
                var data = await _timesheetService.GetActiveEntryAsync(resolvedStaffId.Value);
                return Ok(data);
            }
            catch (NotFoundException ex)
            {
                return this.NotFoundProblem(ex.Message);
            }
            catch (BadRequestException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpPost("check-ins")]
        public async Task<ActionResult<TimesheetEntryDto>> CheckIn([FromBody] TimesheetCheckInRequestDto request)
        {
            var currentUserId = this.GetCurrentUserId();
            if (!currentUserId.HasValue || !this.IsStaffUser())
            {
                return Forbid();
            }

            if (request != null && request.StaffId <= 0)
            {
                request.StaffId = currentUserId.Value;
            }
            else if (request != null && request.StaffId != currentUserId.Value &&
                     !this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "HR_MANAGER", "NHAN_SU", "FOREMAN", "TEAM_LEAD", "QUAN_DOC"))
            {
                return Forbid();
            }

            try
            {
                var data = await _timesheetService.CheckInAsync(request!);
                return Ok(data);
            }
            catch (NotFoundException ex)
            {
                return this.NotFoundProblem(ex.Message);
            }
            catch (BadRequestException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpPost("{id:long}/check-outs")]
        public async Task<ActionResult<TimesheetEntryDto>> CheckOut(long id, [FromBody] TimesheetCheckOutRequestDto request)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var entryOwner = await _timesheetQueryService.GetEntryOwnerAsync(id);
            if (entryOwner != null)
            {
                if (scopedBranchId.HasValue && entryOwner.BranchId != scopedBranchId.Value)
                {
                    return Forbid();
                }

                var currentUserId = this.GetCurrentUserId();
                if (currentUserId.HasValue && entryOwner.StaffId != currentUserId.Value &&
                    !this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "HR_MANAGER", "NHAN_SU", "FOREMAN", "TEAM_LEAD", "QUAN_DOC"))
                {
                    return Forbid();
                }
            }

            try
            {
                var data = await _timesheetService.CheckOutAsync(id, request);
                return Ok(data);
            }
            catch (NotFoundException ex)
            {
                return this.NotFoundProblem(ex.Message);
            }
            catch (BadRequestException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        private async Task<long?> ResolveAccessibleStaffIdAsync(long? requestedStaffId)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out _))
            {
                return null;
            }

            var currentUserId = this.GetCurrentUserId();
            var targetStaffId = requestedStaffId.HasValue && requestedStaffId.Value > 0
                ? requestedStaffId.Value
                : currentUserId ?? 0;

            if (targetStaffId <= 0)
            {
                return null;
            }

            if (currentUserId.HasValue && targetStaffId == currentUserId.Value)
            {
                return targetStaffId;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "HR_MANAGER", "NHAN_SU", "ACCOUNTANT", "FOREMAN", "TEAM_LEAD", "QUAN_DOC"))
            {
                return null;
            }

            if (scopedBranchId.HasValue)
            {
                var targetBranchId = await _timesheetQueryService.GetStaffBranchIdAsync(targetStaffId);
                if (!targetBranchId.HasValue || targetBranchId.Value != scopedBranchId.Value)
                {
                    return null;
                }
            }

            return targetStaffId;
        }
    }
}
