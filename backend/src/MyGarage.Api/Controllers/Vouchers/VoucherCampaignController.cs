// Mục đích: Cung cấp API CRUD chiến dịch voucher và quản lý danh sách voucher áp dụng trong từng chiến dịch.
using System;
using System.Collections.Generic;
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
    [Route("api/v1/voucher-campaigns")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class VoucherCampaignController : ControllerBase
    {
        private const decimal BudgetWarningThresholdPercent = 80m;
        private readonly IVoucherCampaignService _service;
        private readonly IVoucherCampaignQueryService _queryService;

        public VoucherCampaignController(IVoucherCampaignService service, IVoucherCampaignQueryService queryService)
        {
            _service = service;
            _queryService = queryService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<VoucherCampaignResponseDto>>> GetAll(
            [FromQuery] string? keyword = null,
            [FromQuery] string? status = null,
            [FromQuery] bool includeDeleted = false,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _queryService.GetPageAsync(keyword, status, includeDeleted, normalizedPage, normalizedSize, HttpContext.RequestAborted);
            var dtos = _service.MapToDtos(paged);
            return Ok(PagedEnvelope<VoucherCampaignResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        // Server-side list and summaries for the campaigns screen; legacy GET is unchanged.
        [HttpGet("admin-page")]
        public async Task<IActionResult> GetAdminPage([FromQuery] string? search, [FromQuery] string? status,
            [FromQuery] int? page, [FromQuery] int? pageSize, System.Threading.CancellationToken cancellationToken)
        {
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
            var result = await _queryService.GetAdminPageAsync(search, status, current, size, cancellationToken);
            var items = new List<VoucherCampaignResponseDto>();
            foreach (var row in result.Rows)
            {
                var item = _service.MapToDto(row.Campaign);
                item.VoucherCount = row.VoucherCount;
                item.ActiveVouchers = row.ActiveVouchers;
                item.Redeemed = row.Redeemed;
                items.Add(item);
            }
            return Ok(new { items, total = result.Total, statuses = result.Statuses,
                stats = new { totalCampaigns = result.TotalCampaigns, activeCampaigns = result.ActiveCampaigns, totalRedeemed = result.TotalRedeemed, budgetWarningCount = result.BudgetWarningCount },
                budgetWarnings = _service.MapToDtos(result.WarningRows) });
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
            var rows = _service.MapToDtos(items, normalizedThreshold)
                .Where(c => c.IsNearBudgetLimit || (includeOverLimit && c.IsBudgetExceeded))
                .OrderByDescending(c => c.BudgetUsagePercent ?? 0);

            return Ok(rows);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VoucherCampaignResponseDto>> GetById(long id)
        {
            var dto = await _service.GetDetailedDtoByIdAsync(id, includeDeleted: true);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        // Bounded detail for screens that do not need the legacy full voucher collection.
        [HttpGet("{id:long}/summary")]
        public async Task<ActionResult<VoucherCampaignResponseDto>> GetSummary(long id, System.Threading.CancellationToken cancellationToken)
        {
            var summary = await _queryService.GetSummaryAsync(id, cancellationToken);
            if (summary == null) return NotFound();
            var result = _service.MapToDto(summary.Campaign);
            result.VoucherCount = summary.VoucherCount;
            return Ok(result);
        }

        [HttpGet("{id:long}/vouchers")]
        [HttpGet("{id:long}/voucher-page")]
        public async Task<IActionResult> GetVoucherPage(long id, [FromQuery] int? page, [FromQuery] int? pageSize,
            System.Threading.CancellationToken cancellationToken)
        {
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
            var result = await _queryService.GetVoucherPageAsync(id, current, size, cancellationToken);
            if (result == null) return NotFound();
            return Ok(new { items = result.Items, total = result.Total });
        }

        [HttpPost]
        public async Task<ActionResult<VoucherCampaignResponseDto>> Create([FromBody] VoucherCampaignCreateDto dto)
        {
            try
            {
                var response = await _service.CreateDtoAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<VoucherCampaignResponseDto>> Update(long id, [FromBody] VoucherCampaignUpdateDto dto)
        {
            try
            {
                var response = await _service.UpdateDtoAsync(id, dto);
                if (response == null)
                {
                    return NotFound();
                }

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpPost("{id:long}/voucher-links")]
        public async Task<ActionResult<VoucherCampaignResponseDto>> AssignVouchers(long id, [FromBody] VoucherCampaignAssignVouchersDto dto)
        {
            try
            {
                var updated = await _service.AssignVouchersDtoAsync(id, dto.VoucherIds ?? new List<long>(), dto.ReplaceExisting, dto.UpdatedBy);
                if (updated == null)
                {
                    return NotFound();
                }

                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
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
    }
}



