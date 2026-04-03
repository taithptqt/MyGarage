// Mục đích: Truy cập dữ liệu và lọc danh sách yêu cầu phê duyệt voucher.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Repositories
{
    public class VoucherApprovalRequestRepository : GenericRepository<VoucherApprovalRequest>, IVoucherApprovalRequestRepository
    {
        public VoucherApprovalRequestRepository(GarageContext context) : base(context)
        {
        }

        public async Task<IEnumerable<VoucherApprovalRequest>> GetAllAsync(string? status = null)
        {
            var query = _dbSet
                .Include(x => x.Voucher)
                .Include(x => x.Redemption)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToUpperInvariant();
                query = query.Where(x => x.Status != null && x.Status.ToUpper() == normalized);
            }

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .ToListAsync();
        }

        public async Task<VoucherApprovalRequest?> GetDetailedByIdAsync(long id)
        {
            return await _dbSet
                .Include(x => x.Voucher)
                .Include(x => x.Redemption)
                .FirstOrDefaultAsync(x => x.Id == id);
        }
    }
}

