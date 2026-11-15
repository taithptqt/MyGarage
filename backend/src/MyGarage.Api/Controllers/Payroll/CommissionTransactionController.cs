using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/commission-transactions")]
    [Route("api/v1/commission-transactions")]
    [ApiController]
    [Authorize]
    public class CommissionTransactionController : ControllerBase
    {
        private readonly ICommissionTransactionService _commissionTransactionService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public CommissionTransactionController(
            ICommissionTransactionService commissionTransactionService,
            IGarageContext context,
            IMapper mapper)
        {
            _commissionTransactionService = commissionTransactionService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet("/api/commissions")]
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

            var query = _context.CommissionTransactions
                .AsNoTracking()
                .Include(ct => ct.Staff)
                .Include(ct => ct.ServiceOrder)
                .AsQueryable();

            if (scopedBranchId.HasValue)
            {
                query = query.Where(ct =>
                    ct.Staff.BranchId == scopedBranchId.Value ||
                    (ct.ServiceOrder != null && ct.ServiceOrder.ExecuteBranchId == scopedBranchId.Value));
            }

            if (!canViewBranchAll)
            {
                if (!currentUserId.HasValue)
                {
                    return Forbid();
                }

                query = query.Where(ct => ct.StaffId == currentUserId.Value);
            }
            else if (staffId.HasValue && staffId.Value > 0)
            {
                query = query.Where(ct => ct.StaffId == staffId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToUpperInvariant();
                query = query.Where(ct => ct.Status != null && ct.Status.ToUpper() == normalizedStatus);
            }

            if (!string.IsNullOrWhiteSpace(period))
            {
                if (!DateTime.TryParseExact(
                        period.Trim(),
                        "yyyy-MM",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var monthStart))
                {
                    return BadRequest(new { message = "Định dạng period phải là yyyy-MM." });
                }

                var monthEndExclusive = monthStart.AddMonths(1);
                query = query.Where(ct =>
                    (ct.CreatedAt ?? ct.ApprovedAt) >= monthStart &&
                    (ct.CreatedAt ?? ct.ApprovedAt) < monthEndExclusive);
            }

            var rows = await query
                .OrderByDescending(ct => ct.CreatedAt ?? ct.ApprovedAt)
                .ThenByDescending(ct => ct.Id)
                .ToListAsync();

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

            var query = _context.CommissionTransactions.AsNoTracking().AsQueryable();
            if (scopedBranchId.HasValue)
            {
                query = query.Where(ct => ct.Staff.BranchId == scopedBranchId.Value);
            }

            var envelope = await query
                .OrderByDescending(ct => ct.Id)
                .ToPagedEnvelopeAsync(page, pageSize, paged => _mapper.Map<IEnumerable<CommissionTransactionResponseDto>>(paged));
            return Ok(envelope);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<CommissionTransactionResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var item = await _context.CommissionTransactions
                .AsNoTracking()
                .Include(ct => ct.Staff)
                .FirstOrDefaultAsync(ct => ct.Id == id);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && item.Staff?.BranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            var dto = _mapper.Map<CommissionTransactionResponseDto>(item);
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
                var staffBranchId = await _context.Staffs
                    .AsNoTracking()
                    .Where(s => s.Id == itemDto.StaffId)
                    .Select(s => (long?)s.BranchId)
                    .FirstOrDefaultAsync();
                if (!staffBranchId.HasValue || staffBranchId.Value != scopedBranchId.Value)
                {
                    return Forbid();
                }
            }

            var entity = _mapper.Map<CommissionTransaction>(itemDto);
            var createdItem = await _commissionTransactionService.AddAsync(entity);
            var responseDto = _mapper.Map<CommissionTransactionResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
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
                var staffBranchId = await _context.Staffs
                    .AsNoTracking()
                    .Where(s => s.Id == existingItem.StaffId)
                    .Select(s => (long?)s.BranchId)
                    .FirstOrDefaultAsync();
                if (!staffBranchId.HasValue || staffBranchId.Value != scopedBranchId.Value)
                {
                    return Forbid();
                }
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _commissionTransactionService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<CommissionTransactionResponseDto>(updatedItem);
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
                var staffBranchId = await _context.Staffs
                    .AsNoTracking()
                    .Where(s => s.Id == existingItem.StaffId)
                    .Select(s => (long?)s.BranchId)
                    .FirstOrDefaultAsync();
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






