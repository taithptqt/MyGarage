using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/commission-transactions")]
    [ApiController]
    [Authorize]
    public class CommissionTransactionController : ControllerBase
    {
        private readonly ICommissionTransactionService _commissionTransactionService;
        private readonly ICommissionTransactionQueryService _queryService;

        public CommissionTransactionController(
            ICommissionTransactionService commissionTransactionService,
            ICommissionTransactionQueryService queryService)
        {
            _commissionTransactionService = commissionTransactionService;
            _queryService = queryService;
        }

        [HttpGet("/api/v1/commissions")]
        public async Task<IActionResult> GetCommissions(
            [FromQuery] long? branchId = null,
            [FromQuery] string? period = null,
            [FromQuery] long? staffId = null,
            [FromQuery] string? status = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var currentUserId = this.GetCurrentUserId();
            var canViewBranchAll = this.IsStaffWithAnyKeyword(
                "ADMIN", "MANAGER", "SERVICE_MANAGER", "ACCOUNTANT", "FINANCE", "KE_TOAN", "HR_MANAGER", "NHAN_SU", "FOREMAN", "TEAM_LEAD", "QUAN_DOC");

            long? effectiveStaffId = staffId;
            if (!canViewBranchAll)
            {
                if (!currentUserId.HasValue)
                {
                    return Forbid();
                }

                effectiveStaffId = currentUserId.Value;
            }

            DateTime? monthStart = null;
            DateTime? monthEndExclusive = null;
            if (!string.IsNullOrWhiteSpace(period))
            {
                if (!DateTime.TryParseExact(
                        period.Trim(),
                        "yyyy-MM",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedStart))
                {
                    return this.BadRequestProblem("Định dạng period phải là yyyy-MM.");
                }

                monthStart = parsedStart;
                monthEndExclusive = parsedStart.AddMonths(1);
            }

            var rows = await _queryService.GetCommissionsAsync(
                scopedBranchId,
                effectiveStaffId,
                status,
                monthStart,
                monthEndExclusive,
                HttpContext.RequestAborted);

            var items = rows.Select(ct => new
            {
                id = ct.Id,
                staffId = ct.StaffId,
                staffName = ct.Staff?.FullName,
                branchId = ct.Staff?.BranchId,
                serviceOrderId = ct.ServiceOrderId,
                serviceOrderCode = ct.ServiceOrder?.Code,
                serviceStaffAssignmentId = ct.ServiceStaffAssignmentId,
                retailOrderId = ct.RetailOrderId,
                sourceType = ct.SourceType,
                baseAmount = ct.BaseAmount,
                appliedCommissionType = ct.AppliedCommissionType,
                appliedCommissionValue = ct.AppliedCommissionValue,
                appliedSplitRatio = ct.AppliedSplitRatio,
                amount = ct.CommissionAmount,
                commissionAmount = ct.CommissionAmount,
                logType = ct.LogType,
                status = ct.Status,
                approvedAt = ct.ApprovedAt,
                createdAt = ct.CreatedAt ?? ct.ApprovedAt
            }).ToList();

            return Ok(new { items });
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<CommissionTransactionResponseDto>>> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (items, total) = await _queryService.GetPageAsync(scopedBranchId, normalizedPage, normalizedSize, HttpContext.RequestAborted);
            var dtos = _commissionTransactionService.MapToDtos(items);
            return Ok(PagedEnvelope<CommissionTransactionResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<CommissionTransactionResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var item = await _queryService.GetByIdWithStaffAsync(id, HttpContext.RequestAborted);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && item.Staff?.BranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            var dto = _commissionTransactionService.MapToDto(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CommissionTransactionResponseDto>> Create([FromBody] CommissionTransactionCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "ACCOUNTANT", "FINANCE", "KE_TOAN", "HR_MANAGER"))
            {
                return Forbid();
            }

            if (scopedBranchId.HasValue && itemDto != null)
            {
                var staffBranchId = await _queryService.GetStaffBranchIdAsync(itemDto.StaffId, HttpContext.RequestAborted);
                if (!staffBranchId.HasValue || staffBranchId.Value != scopedBranchId.Value)
                {
                    return Forbid();
                }
            }

            var responseDto = await _commissionTransactionService.CreateDtoAsync(itemDto!);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<CommissionTransactionResponseDto>> Update(long id, [FromBody] CommissionTransactionUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "ACCOUNTANT", "FINANCE", "KE_TOAN", "HR_MANAGER"))
            {
                return Forbid();
            }

            var existingItem = await _commissionTransactionService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue)
            {
                var staffBranchId = await _queryService.GetStaffBranchIdAsync(existingItem.StaffId, HttpContext.RequestAborted);
                if (!staffBranchId.HasValue || staffBranchId.Value != scopedBranchId.Value)
                {
                    return Forbid();
                }
            }

            _commissionTransactionService.MapUpdate(itemDto, existingItem);
            var updatedItem = await _commissionTransactionService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _commissionTransactionService.MapToDto(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "ACCOUNTANT"))
            {
                return Forbid();
            }

            var existingItem = await _commissionTransactionService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue)
            {
                var staffBranchId = await _queryService.GetStaffBranchIdAsync(existingItem.StaffId, HttpContext.RequestAborted);
                if (!staffBranchId.HasValue || staffBranchId.Value != scopedBranchId.Value)
                {
                    return Forbid();
                }
            }

            var result = await _commissionTransactionService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}
