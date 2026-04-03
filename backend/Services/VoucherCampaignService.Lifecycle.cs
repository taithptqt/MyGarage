using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class VoucherCampaignService
    {
        public async Task<int> SyncLifecycleStatusesAsync(DateTime? now = null)
        {
            var current = now ?? DateTime.UtcNow;
            var campaigns = await _context.VoucherCampaigns
                .Where(x => x.IsDeleted != true && (x.Status == null || x.Status != "ARCHIVED"))
                .ToListAsync();

            if (campaigns.Count == 0)
            {
                return 0;
            }

            var budgetByCampaign = await _context.Vouchers
                .Where(v => v.CampaignId != null && v.IsDeleted != true)
                .GroupBy(v => v.CampaignId!.Value)
                .Select(g => new { CampaignId = g.Key, BudgetUsed = g.Sum(v => (decimal?)v.BudgetUsed) ?? 0 })
                .ToDictionaryAsync(x => x.CampaignId, x => x.BudgetUsed);

            var changed = 0;
            foreach (var campaign in campaigns)
            {
                var originalStatus = (campaign.Status ?? string.Empty).Trim().ToUpperInvariant();
                var expectedStatus = ResolveStatus(campaign.Status, campaign.StartDate, campaign.EndDate, campaign.IsActive, campaign.IsDeleted, current);

                var hasStatusChange = !string.Equals(originalStatus, expectedStatus, StringComparison.OrdinalIgnoreCase);
                var recalculatedBudget = budgetByCampaign.TryGetValue(campaign.Id, out var totalBudget) ? totalBudget : 0;
                var hasBudgetChange = campaign.BudgetUsed != recalculatedBudget;

                if (!hasStatusChange && !hasBudgetChange)
                {
                    continue;
                }

                campaign.Status = expectedStatus;
                campaign.BudgetUsed = recalculatedBudget;
                campaign.UpdatedAt = current;
                changed++;
            }

            if (changed > 0)
            {
                await _context.SaveChangesAsync();
            }

            return changed;
        }

    }
}
