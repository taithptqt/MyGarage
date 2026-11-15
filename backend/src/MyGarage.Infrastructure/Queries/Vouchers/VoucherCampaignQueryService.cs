using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfVoucherCampaignQueryService(IGarageContext context) : IVoucherCampaignQueryService
{
    public async Task<(IReadOnlyList<VoucherCampaign> Items, int Total)> GetPageAsync(
        string? keyword,
        string? status,
        bool includeDeleted,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.VoucherCampaigns.AsNoTracking().Include(x => x.Vouchers).AsQueryable();
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

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var paged = offset >= total
            ? []
            : await query.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
                .Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (paged, total);
    }

    public async Task<VoucherCampaignAdminPageResult> GetAdminPageAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var all = context.VoucherCampaigns.AsNoTracking().Where(x => !x.IsDeleted);
        var statuses = await all.Select(x => x.Status.Trim().ToUpper()).Distinct()
            .OrderBy(x => x).ToListAsync(ct);
        var totalCampaigns = await all.CountAsync(ct);
        var activeCampaigns = await all.CountAsync(x => x.Status.Trim().ToUpper() == "ACTIVE", ct);
        // Voucher list used by the former UI includes deleted vouchers, so do not silently exclude them here.
        var totalRedeemed = await context.Vouchers.AsNoTracking()
            .Where(x => x.Campaign != null && !x.Campaign.IsDeleted)
            .SumAsync(x => (long?)x.RedeemedCount, ct) ?? 0;

        var warnings = all.Where(x => x.BudgetLimit.HasValue && x.BudgetLimit.Value > 0 &&
            (x.BudgetUsed >= x.BudgetLimit.Value ||
             (Math.Round(x.BudgetUsed / x.BudgetLimit.Value * 100m, 2) >= 80m &&
              Math.Round(x.BudgetUsed / x.BudgetLimit.Value * 100m, 2) < 100m)));
        var budgetWarningCount = await warnings.CountAsync(ct);
        var warningRows = await warnings.OrderByDescending(x => Math.Round(x.BudgetUsed / x.BudgetLimit!.Value * 100m, 2))
            .ThenByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id).Take(6).ToListAsync(ct);

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

        var total = await filtered.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var rows = offset >= total
            ? []
            : await filtered.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
                .Skip((int)offset).Take(pageSize)
                .Select(x => new VoucherCampaignAdminRow(
                    x,
                    x.Vouchers.Count(v => v.IsDeleted != true),
                    x.Vouchers.Count(v => v.IsDeleted != true && (v.Status.Trim().ToUpper() == "ACTIVE" || v.IsActive == true)),
                    x.Vouchers.Sum(v => (long?)v.RedeemedCount) ?? 0))
                .ToListAsync(ct);

        return new VoucherCampaignAdminPageResult(
            rows,
            total,
            statuses,
            totalCampaigns,
            activeCampaigns,
            totalRedeemed,
            budgetWarningCount,
            warningRows);
    }

    public async Task<VoucherCampaignSummaryResult?> GetSummaryAsync(long id, CancellationToken ct = default)
    {
        var campaign = await context.VoucherCampaigns.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (campaign == null) return null;

        var voucherCount = await context.Vouchers.AsNoTracking()
            .CountAsync(x => x.CampaignId == id && x.IsDeleted != true, ct);
        return new VoucherCampaignSummaryResult(campaign, voucherCount);
    }

    public async Task<VoucherCampaignVoucherPageResult?> GetVoucherPageAsync(
        long id,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (!await context.VoucherCampaigns.AsNoTracking().AnyAsync(x => x.Id == id, ct)) return null;

        var query = context.Vouchers.AsNoTracking().Where(x => x.CampaignId == id && x.IsDeleted != true);
        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? []
            : await query
                .OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((int)offset).Take(pageSize)
                .Select(x => new VoucherCampaignVoucherSummaryDto
                {
                    Id = x.Id,
                    Code = x.Code,
                    VoucherName = x.VoucherName,
                    Type = x.Type,
                    Status = x.Status,
                    IsActive = x.IsActive,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate
                })
                .ToListAsync(ct);

        return new VoucherCampaignVoucherPageResult(items, total);
    }
}
