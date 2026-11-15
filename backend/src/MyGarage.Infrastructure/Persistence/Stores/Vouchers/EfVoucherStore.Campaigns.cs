using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Vouchers;

public sealed partial class EfVoucherStore
{
    private const string VoucherCampaignStatusArchived = nameof(VoucherCampaignStatusCode.ARCHIVED);

    public Task<VoucherCampaign?> GetCampaignByIdAsync(long id, bool track = false, bool excludeDeleted = false, CancellationToken ct = default)
    {
        var query = track ? _context.VoucherCampaigns : _context.VoucherCampaigns.AsNoTracking();
        if (excludeDeleted)
        {
            query = query.Where(x => x.IsDeleted != true);
        }
        return query.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<bool> DeleteCampaignUnassigningVouchersAsync(long campaignId, DateTime now, CancellationToken ct = default)
    {
        var campaign = await _context.VoucherCampaigns
            .Include(x => x.Vouchers)
            .FirstOrDefaultAsync(x => x.Id == campaignId, ct);

        if (campaign == null)
        {
            return false;
        }

        foreach (var voucher in campaign.Vouchers)
        {
            voucher.CampaignId = null;
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = campaign.UpdatedBy;
        }

        _context.VoucherCampaigns.Remove(campaign);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<VoucherCampaign>> GetActiveLifecycleCampaignsAsync(CancellationToken ct = default)
    {
        return await _context.VoucherCampaigns
            .Where(x => x.IsDeleted != true && (x.Status == null || x.Status != VoucherCampaignStatusArchived))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> GetVoucherBudgetUsedByCampaignAsync(CancellationToken ct = default)
    {
        return await _context.Vouchers
            .Where(v => v.CampaignId != null && v.IsDeleted != true)
            .GroupBy(v => v.CampaignId!.Value)
            .Select(g => new { CampaignId = g.Key, BudgetUsed = g.Sum(v => (decimal?)v.BudgetUsed) ?? 0 })
            .ToDictionaryAsync(x => x.CampaignId, x => x.BudgetUsed, ct);
    }

    public async Task<IReadOnlyList<Voucher>> GetVouchersToUnassignFromCampaignAsync(long campaignId, IReadOnlyCollection<long> keepVoucherIds, CancellationToken ct = default)
    {
        return await _context.Vouchers
            .Where(v => v.CampaignId == campaignId && !keepVoucherIds.Contains(v.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Voucher>> GetNonDeletedVouchersByIdsAsync(IReadOnlyCollection<long> voucherIds, CancellationToken ct = default)
    {
        return await _context.Vouchers
            .Where(v => voucherIds.Contains(v.Id) && v.IsDeleted != true)
            .ToListAsync(ct);
    }

    public async Task<decimal> GetCampaignVoucherBudgetUsedAsync(long campaignId, CancellationToken ct = default)
    {
        return await _context.Vouchers
            .Where(v => v.CampaignId == campaignId && v.IsDeleted != true)
            .SumAsync(v => (decimal?)v.BudgetUsed, ct) ?? 0m;
    }
}
