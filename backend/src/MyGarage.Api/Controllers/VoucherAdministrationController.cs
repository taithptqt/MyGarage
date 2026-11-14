using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/vouchers/admin-list")]
[Route("api/v1/vouchers/admin-list")]
public sealed class VoucherAdministrationController : ControllerBase
{
    private readonly IGarageContext _db;
    private readonly IMapper _mapper;

    public VoucherAdministrationController(IGarageContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken,
        [FromQuery] bool includeDeleted = true)
    {
        var all = _db.Vouchers.AsNoTracking();
        var statuses = await all.Select(x => x.Status.Trim().ToUpper()).Distinct().OrderBy(x => x)
            .ToListAsync(cancellationToken);
        var query = all;
        if (!includeDeleted) query = query.Where(x => x.IsDeleted != true);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(x => x.Status.Trim().ToUpper() == normalizedStatus);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(x => (x.Code + " " + x.VoucherName + " " + x.Type + " " + (x.Description ?? "null"))
                .ToUpper().Contains(term));
        }
        var total = await query.CountAsync(cancellationToken);
        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var offset = ((long)currentPage - 1) * size;
        var data = offset >= total ? new List<Voucher>() : await query.OrderBy(x => x.Id)
            .Skip((int)offset).Take(size).ToListAsync(cancellationToken);
        return Ok(new { items = _mapper.Map<List<VoucherResponseDto>>(data), total, statuses });
    }

    // Totals and sorting match the legacy voucher report's unfiltered voucher collection.
    [HttpGet("~/api/vouchers/report-page")]
    public async Task<IActionResult> GetReportPage([FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var all = _db.Vouchers.AsNoTracking();
        var total = await all.CountAsync(cancellationToken);
        var issued = await all.SumAsync(x => (long?)x.IssuedCount, cancellationToken) ?? 0;
        var redeemed = await all.SumAsync(x => (long?)x.RedeemedCount, cancellationToken) ?? 0;
        var budgetLimit = await all.SumAsync(x => x.BudgetLimit, cancellationToken) ?? 0m;
        var budgetUsed = await all.SumAsync(x => (decimal?)x.BudgetUsed, cancellationToken) ?? 0m;
        var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var offset = ((long)current - 1) * size;
        var data = offset >= total ? new List<Voucher>() : await all.OrderByDescending(x => x.RedeemedCount)
            .ThenBy(x => x.Id).Skip((int)offset).Take(size).ToListAsync(cancellationToken);
        return Ok(new { items = _mapper.Map<List<VoucherResponseDto>>(data), total, issued, redeemed, budgetLimit, budgetUsed });
    }

    // Global dashboard combines voucher, redemption and approval aggregates and therefore requires admin.
    [Authorize(Policy = "AdminStaffOnly")]
    [HttpGet("~/api/vouchers/dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var vouchers = _db.Vouchers.AsNoTracking();
        var activeCount = await vouchers.CountAsync(x => x.Status.Trim().ToUpper() == "ACTIVE" || x.IsActive == true, cancellationToken);
        var issued = await vouchers.SumAsync(x => (long?)x.IssuedCount, cancellationToken) ?? 0L;
        var redeemed = await vouchers.SumAsync(x => (long?)x.RedeemedCount, cancellationToken) ?? 0L;
        var budgetLimit = await vouchers.SumAsync(x => x.BudgetLimit, cancellationToken) ?? 0m;
        var budgetUsed = await vouchers.SumAsync(x => (decimal?)x.BudgetUsed, cancellationToken) ?? 0m;
        var totalDiscount = await _db.VoucherRedemptions.AsNoTracking()
            .SumAsync(x => (decimal?)x.DiscountAmount, cancellationToken) ?? 0m;
        var pending = _db.VoucherApprovalRequests.AsNoTracking().Where(x => x.Status.Trim().ToUpper() == "PENDING");
        var pendingApprovals = await pending.CountAsync(cancellationToken);
        var approvalItems = await pending.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(6).Select(x => new { x.Id, x.RequestType, x.RiskLevel }).ToListAsync(cancellationToken);
        var top = await vouchers.OrderByDescending(x => x.RedeemedCount).ThenBy(x => x.Id)
            .Take(8).ToListAsync(cancellationToken);
        var near = await vouchers.Where(x => x.IssuedCount > 0 && (decimal)x.RedeemedCount / x.IssuedCount >= 0.8m)
            .OrderByDescending(x => (decimal)x.RedeemedCount / x.IssuedCount).ThenBy(x => x.Id)
            .Take(8).Select(x => new { x.Id, x.Code, x.VoucherName, ratio = (decimal)x.RedeemedCount / x.IssuedCount })
            .ToListAsync(cancellationToken);
        return Ok(new { activeCount, issued, redeemed, budgetLimit, budgetUsed, totalDiscount, pendingApprovals,
            topVouchers = _mapper.Map<List<VoucherResponseDto>>(top), nearQuotaVouchers = near, approvalItems });
    }
}
