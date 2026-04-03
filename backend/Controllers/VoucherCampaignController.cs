// Mục đích: Cung cấp API CRUD chiến dịch voucher và quản lý danh sách voucher áp dụng trong từng chiến dịch.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/voucher-campaigns")]
    [ApiController]
    public class VoucherCampaignController : ControllerBase
    {
        private const decimal BudgetWarningThresholdPercent = 80m;
        private readonly IVoucherCampaignService _service;
        private readonly IMapper _mapper;

        public VoucherCampaignController(IVoucherCampaignService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VoucherCampaignResponseDto>>> GetAll(
            [FromQuery] string? keyword = null,
            [FromQuery] string? status = null,
            [FromQuery] bool includeDeleted = false)
        {
            var items = await _service.GetAllAsync(keyword, status, includeDeleted);
            return Ok(items.Select(item => MapResponse(item)));
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



