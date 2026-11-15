using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherCampaignService
    {
        public async Task<VoucherCampaign?> AssignVouchersAsync(
            long campaignId,
            IEnumerable<long> voucherIds,
            bool replaceExisting,
            long? updatedBy = null)
        {
            var campaign = await _context.VoucherCampaigns.FirstOrDefaultAsync(x => x.Id == campaignId && x.IsDeleted != true);
            if (campaign == null)
            {
                return null;
            }

            var normalizedIds = voucherIds
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            var now = VietnamTime.Now;

            if (replaceExisting)
            {
                var toUnassign = await _context.Vouchers
                    .Where(v => v.CampaignId == campaignId && !normalizedIds.Contains(v.Id))
                    .ToListAsync();

                foreach (var voucher in toUnassign)
                {
                    voucher.CampaignId = null;
                    voucher.UpdatedAt = now;
                    voucher.UpdatedBy = updatedBy;
                }
            }

            if (normalizedIds.Count > 0)
            {
                var vouchers = await _context.Vouchers
                    .Where(v => normalizedIds.Contains(v.Id) && v.IsDeleted != true)
                    .ToListAsync();

                if (vouchers.Count != normalizedIds.Count)
                {
                    throw new InvalidOperationException("Danh s\u00e1ch voucher c\u00f3 m\u00e3 kh\u00f4ng t\u1ed3n t\u1ea1i ho\u1eb7c \u0111\u00e3 b\u1ecb x\u00f3a.");
                }

                foreach (var voucher in vouchers)
                {
                    voucher.CampaignId = campaignId;
                    voucher.UpdatedAt = now;
                    voucher.UpdatedBy = updatedBy;
                }
            }

            campaign.UpdatedAt = now;
            campaign.UpdatedBy = updatedBy;
            campaign.BudgetUsed = await _context.Vouchers
                .Where(v => v.CampaignId == campaignId && v.IsDeleted != true)
                .SumAsync(v => (decimal?)v.BudgetUsed) ?? 0;
            campaign.Status = ResolveStatus(campaign.Status, campaign.StartDate, campaign.EndDate, campaign.IsActive, campaign.IsDeleted, now);

            await _context.SaveChangesAsync();
            return await _voucherCampaignRepository.GetDetailedByIdAsync(campaignId, includeDeleted: true);
        }
    }
}