// Mục đích: Truy cập dữ liệu lịch sử redeem voucher và tra cứu idempotency.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Repositories
{
    public class VoucherRedemptionRepository : GenericRepository<VoucherRedemption>, IVoucherRedemptionRepository
    {
        public VoucherRedemptionRepository(GarageContext context) : base(context)
        {
        }

        public async Task<IEnumerable<VoucherRedemption>> GetAllAsync(string? status = null)
        {
            var query = _dbSet
                .Include(x => x.Voucher)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                query = query.Where(x => x.RedemptionStatus != null && x.RedemptionStatus.ToUpper() == normalized);
            }

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .ToListAsync();
        }

        public async Task<VoucherRedemption?> GetDetailedByIdAsync(long id)
        {
            return await _dbSet
                .Include(x => x.Voucher)
                .Include(x => x.ApprovalRequest)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<VoucherRedemption?> GetByIdempotencyKeyAsync(string idempotencyKey)
        {
            return await _dbSet
                .Include(x => x.Voucher)
                .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey);
        }
    }
}

