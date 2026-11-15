// Mục đích: Cung cấp API CRUD chiến dịch voucher và quản lý danh sách voucher áp dụng trong từng chiến dịch.
using System;
using System.Collections.Generic;
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
    [Route("api/voucher-campaigns")]
    [Route("api/v1/voucher-campaigns")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class VoucherCampaignController : ControllerBase
    {
        private const decimal BudgetWarningThresholdPercent = 80m;
        private readonly IVoucherCampaignService _service;
        private readonly IMapper _mapper;
        private readonly IGarageContext _context;

        public VoucherCampaignController(IVoucherCampaignService service, IMapper mapper, IGarageContext context)
        {
            _service = service;
            _mapper = mapper;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<VoucherCampaignResponseDto>>> GetAll(
            [FromQuery] string? keyword = null,
            [FromQuery] string? status = null,
            [FromQuery] bool includeDeleted = false,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var query = _context.VoucherCampaigns.AsNoTracking().Include(x => x.Vouchers).AsQueryable();
            if (!includeDeleted) query = query.Where(x => x.IsDeleted != true);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalized = keyword.Trim().ToUpperInvariant();
                query = query.Where(x => (x.Name != null && x.Name.ToUpper().Contains(normalized)) ||
                    (x.CampaignCode != null && x.CampaignCode.ToUpper().Contains(normalized)) ||
                    (x.Objective != null && x.Objective.ToUpper().Contains(normalized)) ||
                    (x.Description != null && x.Description.ToUpper().Contains(normalized)));
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                query = query.Where(x => x.Status != null && x.Status.ToUpper() == normalized);
            }
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var total = await query.CountAsync();
            var offset = ((long)normalizedPage - 1) * normalizedSize;
            var paged = offset >= total
                ? new List<VoucherCampaign>()
                : await query.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
                    .Skip((int)offset).Take(normalizedSize).ToListAsync();
            var dtos = paged.Select(item => MapResponse(item)).ToList();
            return Ok(PagedEnvelope<VoucherCampaignResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        // Server-side list and summaries for the campaigns screen; legacy GET is unchanged.
        [HttpGet("admin-page")]
        public async Task<IActionResult> GetAdminPage([FromQuery] string? search, [FromQuery] string? status,
            [FromQuery] int? page, [FromQuery] int? pageSize, System.Threading.CancellationToken cancellationToken)
        {
            var all = _context.VoucherCampaigns.AsNoTracking().Where(x => !x.IsDeleted);
            var statuses = await all.Select(x => x.Status.Trim().ToUpper()).Distinct()
                .OrderBy(x => x).ToListAsync(cancellationToken);
            var totalCampaigns = await all.CountAsync(cancellationToken);
            var activeCampaigns = await all.CountAsync(x => x.Status.Trim().ToUpper() == "ACTIVE", cancellationToken);
            // Voucher list used by the former UI includes deleted vouchers, so do not silently exclude them here.
            var totalRedeemed = await _context.Vouchers.AsNoTracking()
                .Where(x => x.Campaign != null && !x.Campaign.IsDeleted)
                .SumAsync(x => (long?)x.RedeemedCount, cancellationToken) ?? 0;
            var warnings = all.Where(x => x.BudgetLimit.HasValue && x.BudgetLimit.Value > 0 &&
                (x.BudgetUsed >= x.BudgetLimit.Value ||
                 (Math.Round(x.BudgetUsed / x.BudgetLimit.Value * 100m, 2) >= 80m &&
                  Math.Round(x.BudgetUsed / x.BudgetLimit.Value * 100m, 2) < 100m)));
            var budgetWarningCount = await warnings.CountAsync(cancellationToken);
            var warningRows = await warnings.OrderByDescending(x => Math.Round(x.BudgetUsed / x.BudgetLimit!.Value * 100m, 2))
                .ThenByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id).Take(6).ToListAsync(cancellationToken);
            var filtered = all;
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var token = status.Trim().ToUpperInvariant();
                filtered = filtered.Where(x => x.Status.Trim().ToUpper() == token);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToUpperInvariant();
                filtered = filtered.Where(x => (x.Name + " " + (x.CampaignCode ?? "null") + " " +
                    (x.Description ?? "null") + " " + (x.Objective ?? "null")).ToUpper().Contains(term));
            }
            var total = await filtered.CountAsync(cancellationToken);
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
            var offset = ((long)current - 1) * size;
            var rows = offset >= total ? null : await filtered.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
                .Skip((int)offset).Take(size)
                .Select(x => new {
                    Campaign = x,
                    VoucherCount = x.Vouchers.Count(v => v.IsDeleted != true),
                    ActiveVouchers = x.Vouchers.Count(v => v.IsDeleted != true && (v.Status.Trim().ToUpper() == "ACTIVE" || v.IsActive == true)),
                    Redeemed = x.Vouchers.Sum(v => (long?)v.RedeemedCount) ?? 0
                }).ToListAsync(cancellationToken);
            var items = new List<VoucherCampaignResponseDto>();
            if (rows != null)
                foreach (var row in rows)
                {
                    var item = MapResponse(row.Campaign);
                    item.VoucherCount = row.VoucherCount;
                    item.ActiveVouchers = row.ActiveVouchers;
                    item.Redeemed = row.Redeemed;
                    items.Add(item);
                }
            return Ok(new { items, total, statuses,
                stats = new { totalCampaigns, activeCampaigns, totalRedeemed, budgetWarningCount },
                budgetWarnings = warningRows.Select(x => MapResponse(x)).ToList() });
        }

        [HttpGet("budget-monitors")]
        public async Task<ActionResult<IEnumerable<VoucherCampaignResponseDto>>> GetBudgetAlerts(
            [FromQuery] decimal thresholdPercent = BudgetWarningThresholdPercent,
            [FromQuery] bool includeOverLimit = true)
        {
            var normalizedThreshold = thresholdPercent <= 0 || thresholdPercent > 100
                ? BudgetWarningThresholdPercent
                : thresholdPercent;

            var items = await _service.GetAllAsync();
            var rows = items
                .Select(c => MapResponse(c, normalizedThreshold))
                .Where(c => c.IsNearBudgetLimit || (includeOverLimit && c.IsBudgetExceeded))
                .OrderByDescending(c => c.BudgetUsagePercent ?? 0);

            return Ok(rows);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VoucherCampaignResponseDto>> GetById(long id)
        {
            var item = await _service.GetDetailedByIdAsync(id, includeDeleted: true);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(MapResponse(item));
        }

        // Bounded detail for screens that do not need the legacy full voucher collection.
        [HttpGet("{id:long}/summary")]
        public async Task<ActionResult<VoucherCampaignResponseDto>> GetSummary(long id, System.Threading.CancellationToken cancellationToken)
        {
            var campaign = await _context.VoucherCampaigns.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (campaign == null) return NotFound();
            var result = MapResponse(campaign);
            result.VoucherCount = await _context.Vouchers.AsNoTracking()
                .CountAsync(x => x.CampaignId == id && x.IsDeleted != true, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id:long}/voucher-page")]
        public async Task<IActionResult> GetVoucherPage(long id, [FromQuery] int? page, [FromQuery] int? pageSize,
            System.Threading.CancellationToken cancellationToken)
        {
            if (!await _context.VoucherCampaigns.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken)) return NotFound();
            var query = _context.Vouchers.AsNoTracking().Where(x => x.CampaignId == id && x.IsDeleted != true);
            var total = await query.CountAsync(cancellationToken);
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
            var offset = ((long)current - 1) * size;
            var items = offset >= total ? new List<VoucherCampaignVoucherSummaryDto>() : await query
                .OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((int)offset).Take(size)
                .Select(x => new VoucherCampaignVoucherSummaryDto { Id = x.Id, Code = x.Code, VoucherName = x.VoucherName,
                    Type = x.Type, Status = x.Status, IsActive = x.IsActive, StartDate = x.StartDate, EndDate = x.EndDate })
                .ToListAsync(cancellationToken);
            return Ok(new { items, total });
        }

        [HttpPost]
        public async Task<ActionResult<VoucherCampaignResponseDto>> Create([FromBody] VoucherCampaignCreateDto dto)
        {
            try
            {
                var entity = _mapper.Map<VoucherCampaign>(dto);
                var created = await _service.AddAsync(entity);

                if (dto.VoucherIds != null)
                {
                    await _service.AssignVouchersAsync(
                        created.Id,
                        dto.VoucherIds,
                        dto.ReplaceVoucherScope ?? true,
                        dto.UpdatedBy ?? dto.CreatedBy);
                }

                var detailed = await _service.GetDetailedByIdAsync(created.Id, includeDeleted: true);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapResponse(detailed ?? created));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<VoucherCampaignResponseDto>> Update(long id, [FromBody] VoucherCampaignUpdateDto dto)
        {
            var existing = await _service.GetDetailedByIdAsync(id, includeDeleted: true);
            if (existing == null)
            {
                return NotFound();
            }

            try
            {
                _mapper.Map(dto, existing);
                var updated = await _service.UpdateAsync(id, existing);
                if (updated == null)
                {
                    return NotFound();
                }

                if (dto.VoucherIds != null)
                {
                    await _service.AssignVouchersAsync(id, dto.VoucherIds, dto.ReplaceVoucherScope ?? true, dto.UpdatedBy);
                }

                var detailed = await _service.GetDetailedByIdAsync(id, includeDeleted: true);
                return Ok(MapResponse(detailed ?? updated));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id:long}/voucher-links")]
        public async Task<ActionResult<VoucherCampaignResponseDto>> AssignVouchers(long id, [FromBody] VoucherCampaignAssignVouchersDto dto)
        {
            try
            {
                var updated = await _service.AssignVouchersAsync(id, dto.VoucherIds ?? new List<long>(), dto.ReplaceExisting, dto.UpdatedBy);
                if (updated == null)
                {
                    return NotFound();
                }

                return Ok(MapResponse(updated));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        private static VoucherCampaignResponseDto MapResponse(VoucherCampaign campaign, decimal budgetWarningThresholdPercent = BudgetWarningThresholdPercent)
        {
            var vouchers = campaign.Vouchers?
                .Where(v => v.IsDeleted != true)
                .OrderBy(v => v.Code)
                .ThenBy(v => v.Id)
                .Select(v => new VoucherCampaignVoucherSummaryDto
                {
                    Id = v.Id,
                    Code = v.Code,
                    VoucherName = v.VoucherName,
                    Type = v.Type,
                    Status = v.Status,
                    IsActive = v.IsActive,
                    StartDate = v.StartDate,
                    EndDate = v.EndDate
                })
                .ToList() ?? new List<VoucherCampaignVoucherSummaryDto>();

            var budgetLimit = campaign.BudgetLimit ?? 0;
            var budgetUsed = campaign.BudgetUsed;
            var budgetRemaining = budgetLimit > 0 ? Math.Max(0, budgetLimit - budgetUsed) : 0;
            var budgetUsagePercent = budgetLimit > 0
                ? Math.Round((budgetUsed / budgetLimit) * 100, 2)
                : 0;
            var isBudgetExceeded = budgetLimit > 0 && budgetUsed >= budgetLimit;
            var isNearBudgetLimit = budgetLimit > 0
                && budgetUsagePercent >= budgetWarningThresholdPercent
                && budgetUsagePercent < 100;
            var budgetAlertLevel = budgetLimit <= 0
                ? "NO_LIMIT"
                : isBudgetExceeded
                    ? "OVER_LIMIT"
                    : isNearBudgetLimit
                        ? "NEAR_LIMIT"
                        : "NORMAL";

            return new VoucherCampaignResponseDto
            {
                Id = campaign.Id,
                CampaignCode = campaign.CampaignCode,
                Name = campaign.Name,
                Objective = campaign.Objective,
                Description = campaign.Description,
                StartDate = campaign.StartDate,
                EndDate = campaign.EndDate,
                BudgetLimit = campaign.BudgetLimit,
                BudgetUsed = campaign.BudgetUsed,
                BudgetRemaining = budgetRemaining,
                BudgetUsagePercent = budgetUsagePercent,
                IsNearBudgetLimit = isNearBudgetLimit,
                IsBudgetExceeded = isBudgetExceeded,
                BudgetAlertLevel = budgetAlertLevel,
                Status = campaign.Status,
                OwnerStaffId = campaign.OwnerStaffId,
                IsActive = campaign.IsActive,
                CreatedAt = campaign.CreatedAt,
                CreatedBy = campaign.CreatedBy,
                UpdatedAt = campaign.UpdatedAt,
                UpdatedBy = campaign.UpdatedBy,
                IsDeleted = campaign.IsDeleted,
                DeletedAt = campaign.DeletedAt,
                DeletedBy = campaign.DeletedBy,
                VoucherCount = vouchers.Count,
                VoucherIds = vouchers.Select(v => v.Id).ToList(),
                Vouchers = vouchers
            };
        }
    }
}



