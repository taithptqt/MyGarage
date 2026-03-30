// Mục đích: Truy cập dữ liệu chiến dịch voucher, hỗ trợ lọc theo từ khóa/trạng thái và lấy chi tiết kèm voucher.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Repositories
{
    public class VoucherCampaignRepository : GenericRepository<VoucherCampaign>, IVoucherCampaignRepository
    {
        public VoucherCampaignRepository(GarageContext context) : base(context)
        {
        }

        public async Task<IEnumerable<VoucherCampaign>> GetAllAsync(string? keyword = null, string? status = null, bool includeDeleted = false)
        {
            var query = _dbSet
                .Include(x => x.Vouchers)
                .AsQueryable();

            if (!includeDeleted)
            {
                query = query.Where(x => x.IsDeleted != true);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalized = keyword.Trim().ToUpperInvariant();
                query = query.Where(x =>
                    (x.Name != null && x.Name.ToUpper().Contains(normalized)) ||
                    (x.CampaignCode != null && x.CampaignCode.ToUpper().Contains(normalized)) ||
                    (x.Objective != null && x.Objective.ToUpper().Contains(normalized)) ||
                    (x.Description != null && x.Description.ToUpper().Contains(normalized)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToUpperInvariant();
                query = query.Where(x => x.Status != null && x.Status.ToUpper() == normalizedStatus);
            }

            return await query
                .OrderByDescending(x => x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .ToListAsync();
        }

        public async Task<VoucherCampaign?> GetDetailedByIdAsync(long id, bool includeDeleted = false)
        {
            var query = _dbSet
                .Include(x => x.Vouchers)
                .AsQueryable();

            if (!includeDeleted)
            {
                query = query.Where(x => x.IsDeleted != true);
            }

            return await query.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<bool> CampaignCodeExistsAsync(string campaignCode, long? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(campaignCode))
            {
                return false;
            }

            var normalized = campaignCode.Trim().ToUpperInvariant();
            var query = _dbSet.Where(x => x.CampaignCode != null && x.CampaignCode.ToUpper() == normalized);

            if (excludeId.HasValue)
            {
                query = query.Where(x => x.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
