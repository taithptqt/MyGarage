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
        public override async Task<VoucherCampaign> AddAsync(VoucherCampaign entity)
        {
            var now = DateTime.UtcNow;
            NormalizeAndValidate(entity, now);
            if (entity.CreatedAt == default) entity.CreatedAt = now;
            if (entity.UpdatedAt == default) entity.UpdatedAt = now;

            if (!string.IsNullOrWhiteSpace(entity.CampaignCode) &&
                await _voucherCampaignRepository.CampaignCodeExistsAsync(entity.CampaignCode))
            {
                throw new InvalidOperationException("M\u00e3 chi\u1ebfn d\u1ecbch \u0111\u00e3 t\u1ed3n t\u1ea1i.");
            }

            return await base.AddAsync(entity);
        }

        public override async Task<VoucherCampaign?> UpdateAsync(object id, VoucherCampaign entity)
        {
            if (id is not long campaignId)
            {
                return await base.UpdateAsync(id, entity);
            }

            var existing = await _context.VoucherCampaigns.FirstOrDefaultAsync(x => x.Id == campaignId);
            if (existing == null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            NormalizeAndValidate(entity, now);
            entity.Id = campaignId;
            entity.CreatedAt = existing.CreatedAt;
            entity.CreatedBy = existing.CreatedBy;
            entity.UpdatedAt = now;

            if (!string.IsNullOrWhiteSpace(entity.CampaignCode) &&
                await _voucherCampaignRepository.CampaignCodeExistsAsync(entity.CampaignCode, campaignId))
            {
                throw new InvalidOperationException("M\u00e3 chi\u1ebfn d\u1ecbch \u0111\u00e3 t\u1ed3n t\u1ea1i.");
            }

            return await base.UpdateAsync(campaignId, entity);
        }

        public override async Task<bool> DeleteAsync(object id)
        {
            if (id is not long campaignId)
            {
                return await base.DeleteAsync(id);
            }

            var campaign = await _context.VoucherCampaigns
                .Include(x => x.Vouchers)
                .FirstOrDefaultAsync(x => x.Id == campaignId);

            if (campaign == null)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            foreach (var voucher in campaign.Vouchers)
            {
                voucher.CampaignId = null;
                voucher.UpdatedAt = now;
                voucher.UpdatedBy = campaign.UpdatedBy;
            }

            _context.VoucherCampaigns.Remove(campaign);
            await _context.SaveChangesAsync();
            return true;
        }

    }
}