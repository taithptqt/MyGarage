using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/vouchers")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class VoucherController : ControllerBase
    {
        private const string VoucherStatusActive = nameof(VoucherStatusCode.ACTIVE);
        private const string VoucherStatusPaused = nameof(VoucherStatusCode.PAUSED);
        private const string VoucherStatusRevoked = nameof(VoucherStatusCode.REVOKED);
        private const string VoucherStatusArchived = nameof(VoucherStatusCode.ARCHIVED);
        private const string VoucherStatusExpired = nameof(VoucherStatusCode.EXPIRED);
        private const string VoucherStatusScheduled = nameof(VoucherStatusCode.SCHEDULED);
        private const string VoucherStatusExhausted = nameof(VoucherStatusCode.EXHAUSTED);

        public sealed record VoucherStatusPatchDto
        {
            public string? Status { get; init; }
        }

        private readonly IVoucherService _voucherService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public VoucherController(IVoucherService voucherService, IGarageContext context, IMapper mapper)
        {
            _voucherService = voucherService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VoucherResponseDto>>> GetAll()
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<Voucher> query = _context.Vouchers.AsNoTracking();
            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(voucher =>
                    _context.Staffs.Any(staff => staff.Id == voucher.CreatedBy && staff.BranchId == branchId) ||
                    _context.VoucherCampaigns.Any(campaign =>
                        campaign.Id == voucher.CampaignId &&
                        _context.Staffs.Any(staff => staff.Id == campaign.OwnerStaffId && staff.BranchId == branchId)) ||
                    _context.VoucherRedemptions.Any(redemption => redemption.VoucherId == voucher.Id && redemption.BranchId == branchId));
            }

            var items = await query.ToListAsync();
            var dtos = _mapper.Map<IEnumerable<VoucherResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<VoucherResponseDto>> GetById(long id)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var item = await _voucherService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                var inBranch = await _context.Vouchers.AsNoTracking()
                    .Where(voucher => voucher.Id == id)
                    .AnyAsync(voucher =>
                        _context.Staffs.Any(staff => staff.Id == voucher.CreatedBy && staff.BranchId == branchId) ||
                        _context.VoucherCampaigns.Any(campaign =>
                            campaign.Id == voucher.CampaignId &&
                            _context.Staffs.Any(staff => staff.Id == campaign.OwnerStaffId && staff.BranchId == branchId)) ||
                        _context.VoucherRedemptions.Any(redemption => redemption.VoucherId == voucher.Id && redemption.BranchId == branchId));
                if (!inBranch) return NotFound();
            }

            var dto = _mapper.Map<VoucherResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<VoucherResponseDto>> Create([FromBody] VoucherCreateDto itemDto)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            if (itemDto == null)
            {
                return BadRequest(new { message = "Voucher payload is required." });
            }

            var entity = _mapper.Map<Voucher>(itemDto);
            entity.VoucherName = string.IsNullOrWhiteSpace(entity.VoucherName) ? entity.Code ?? string.Empty : entity.VoucherName;
            entity.CreatedBy = actorUserId.Value;
            entity.UpdatedBy = actorUserId.Value;
            entity.CreatedAt = VietnamTime.Now;
            entity.UpdatedAt = VietnamTime.Now;
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.DeletedBy = null;
            entity.UsedCount = 0;
            entity.IssuedCount = 0;
            entity.RedeemedCount = 0;
            entity.BudgetUsed = 0m;
            entity.Status = ResolveStatus(entity);
            entity.IsActive = !string.Equals(entity.Status, VoucherStatusRevoked, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(entity.Status, VoucherStatusArchived, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(entity.Status, VoucherStatusExpired, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(entity.Status, VoucherStatusExhausted, StringComparison.OrdinalIgnoreCase);

            if (scopedBranchId.HasValue && entity.CampaignId.HasValue && entity.CampaignId.Value > 0)
            {
                var campaignInBranch = await _context.VoucherCampaigns.AsNoTracking()
                    .AnyAsync(campaign =>
                        campaign.Id == entity.CampaignId.Value &&
                        _context.Staffs.Any(staff => staff.Id == campaign.OwnerStaffId && staff.BranchId == scopedBranchId.Value));
                if (!campaignInBranch)
                {
                    return NotFound();
                }
            }

            var createdItem = await _voucherService.AddAsync(entity);

            await WriteAuditAsync(
                actorUserId.Value,
                scopedBranchId,
                "VOUCHER_CREATED",
                createdItem.Id,
                null,
                JsonSerializer.Serialize(new
                {
                    createdItem.Code,
                    createdItem.VoucherName,
                    createdItem.Status,
                    createdItem.IsActive,
                    createdItem.BudgetLimit,
                    createdItem.BudgetUsed,
                    createdItem.CreatedBy
                }));

            var responseDto = _mapper.Map<VoucherResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<VoucherResponseDto>> Update(long id, [FromBody] VoucherUpdateDto itemDto)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var existingItem = await _voucherService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue)
            {
                var inBranch = await _context.Vouchers.AsNoTracking()
                    .Where(voucher => voucher.Id == id)
                    .AnyAsync(voucher =>
                        _context.Staffs.Any(staff => staff.Id == voucher.CreatedBy && staff.BranchId == scopedBranchId.Value) ||
                        _context.VoucherRedemptions.Any(redemption => redemption.VoucherId == voucher.Id && redemption.BranchId == scopedBranchId.Value));
                if (!inBranch) return NotFound();
            }

            var oldSnapshot = JsonSerializer.Serialize(new
            {
                existingItem.Code,
                existingItem.VoucherName,
                existingItem.Status,
                existingItem.IsActive,
                existingItem.BudgetUsed,
                existingItem.UpdatedBy
            });

            var immutableUsedCount = existingItem.UsedCount;
            var immutableIssuedCount = existingItem.IssuedCount;
            var immutableRedeemedCount = existingItem.RedeemedCount;
            var immutableBudgetUsed = existingItem.BudgetUsed;
            var immutableCreatedBy = existingItem.CreatedBy;
            var immutableCreatedAt = existingItem.CreatedAt;
            var immutableIsDeleted = existingItem.IsDeleted;
            var immutableDeletedAt = existingItem.DeletedAt;
            var immutableDeletedBy = existingItem.DeletedBy;

            _mapper.Map(itemDto, existingItem);

            existingItem.UsedCount = immutableUsedCount;
            existingItem.IssuedCount = immutableIssuedCount;
            existingItem.RedeemedCount = immutableRedeemedCount;
            existingItem.BudgetUsed = immutableBudgetUsed;
            existingItem.CreatedBy = immutableCreatedBy;
            existingItem.CreatedAt = immutableCreatedAt;
            existingItem.IsDeleted = immutableIsDeleted;
            existingItem.DeletedAt = immutableDeletedAt;
            existingItem.DeletedBy = immutableDeletedBy;
            existingItem.UpdatedBy = actorUserId.Value;
            existingItem.UpdatedAt = VietnamTime.Now;
            existingItem.Status = ResolveStatus(existingItem);
            existingItem.IsActive = !string.Equals(existingItem.Status, VoucherStatusRevoked, StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(existingItem.Status, VoucherStatusArchived, StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(existingItem.Status, VoucherStatusExpired, StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(existingItem.Status, VoucherStatusExhausted, StringComparison.OrdinalIgnoreCase);

            var updatedItem = await _voucherService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            await WriteAuditAsync(
                actorUserId.Value,
                scopedBranchId,
                "VOUCHER_UPDATED",
                updatedItem.Id,
                oldSnapshot,
                JsonSerializer.Serialize(new
                {
                    updatedItem.Code,
                    updatedItem.VoucherName,
                    updatedItem.Status,
                    updatedItem.IsActive,
                    updatedItem.BudgetUsed,
                    updatedItem.UpdatedBy
                }));

            var responseDto = _mapper.Map<VoucherResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpPatch("{id}")]
        public async Task<ActionResult<VoucherResponseDto>> PatchStatus(long id, [FromBody] VoucherStatusPatchDto dto)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var status = (dto?.Status ?? string.Empty).Trim().ToUpperInvariant();
            return status switch
            {
                VoucherStatusActive => await UpdateStatusAsync(id, VoucherStatusActive, true, actorUserId.Value, scopedBranchId),
                VoucherStatusPaused => await UpdateStatusAsync(id, VoucherStatusPaused, false, actorUserId.Value, scopedBranchId),
                VoucherStatusRevoked => await UpdateStatusAsync(id, VoucherStatusRevoked, false, actorUserId.Value, scopedBranchId),
                _ => BadRequest(new { message = "Status must be ACTIVE, PAUSED, or REVOKED." }),
            };
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!TryResolveFinanceBranchScope(out var scopedBranchId, out var denied)) return denied;

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var existingItem = await _voucherService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var oldSnapshot = JsonSerializer.Serialize(new
            {
                existingItem.Code,
                existingItem.Status,
                existingItem.IsActive
            });

            var result = await _voucherService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            await WriteAuditAsync(actorUserId.Value, scopedBranchId, "VOUCHER_DELETED", id, oldSnapshot, null);
            return NoContent();
        }

        private async Task<ActionResult<VoucherResponseDto>> UpdateStatusAsync(long id, string status, bool isActive, long actorUserId, long? branchId)
        {
            var existingItem = await _voucherService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var oldSnapshot = JsonSerializer.Serialize(new
            {
                existingItem.Status,
                existingItem.IsActive
            });

            existingItem.Status = status;
            existingItem.IsActive = isActive;
            existingItem.UpdatedAt = VietnamTime.Now;
            existingItem.UpdatedBy = actorUserId;

            var updatedItem = await _voucherService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            await WriteAuditAsync(
                actorUserId,
                branchId,
                "VOUCHER_STATUS_CHANGED",
                updatedItem.Id,
                oldSnapshot,
                JsonSerializer.Serialize(new
                {
                    updatedItem.Status,
                    updatedItem.IsActive,
                    updatedItem.UpdatedBy
                }));

            return Ok(_mapper.Map<VoucherResponseDto>(updatedItem));
        }

        private async Task WriteAuditAsync(
            long actorUserId,
            long? branchId,
            string action,
            long recordId,
            string? oldValue,
            string? newValue)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                UserType = "STAFF",
                BranchId = branchId,
                Action = action,
                TableName = "VOUCHERS",
                RecordId = recordId,
                OldValue = oldValue,
                NewValue = newValue,
                Timestamp = VietnamTime.Now
            });
            await _context.SaveChangesAsync();
        }

        private bool TryResolveFinanceBranchScope(out long? branchId, out ActionResult deniedResult)
        {
            branchId = null;
            deniedResult = null!;

            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                deniedResult = Unauthorized();
                return false;
            }

            if (this.RequiresFinanceBranchScope())
            {
                branchId = this.GetCurrentBranchId();
                if (!branchId.HasValue)
                {
                    deniedResult = Forbid();
                    return false;
                }
            }

            return true;
        }

        private static string ResolveStatus(Voucher voucher)
        {
            var now = VietnamTime.Now;

            if (voucher.IsDeleted == true)
            {
                return VoucherStatusArchived;
            }

            if (voucher.IsActive == false)
            {
                return VoucherStatusPaused;
            }

            if (voucher.EndDate < now)
            {
                return VoucherStatusExpired;
            }

            if (voucher.StartDate > now)
            {
                return VoucherStatusScheduled;
            }

            if (voucher.UsageLimit.HasValue && voucher.UsageLimit > 0 && voucher.UsedCount.HasValue && voucher.UsedCount >= voucher.UsageLimit)
            {
                return VoucherStatusExhausted;
            }

            if (voucher.BudgetLimit.HasValue && voucher.BudgetUsed >= voucher.BudgetLimit.Value)
            {
                return VoucherStatusExhausted;
            }

            return VoucherStatusActive;
        }
    }
}
