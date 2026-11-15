using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Common
{
    public sealed class EfSharedOperationsAuthorizationStore : ISharedOperationsAuthorizationStore
    {
        private readonly IGarageContext _context;

        public EfSharedOperationsAuthorizationStore(IGarageContext context)
        {
            _context = context;
        }

        public Task<ApprovalRequest?> GetApprovalRequestByIdAsync(long approvalRequestId, CancellationToken ct = default)
        {
            return _context.ApprovalRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == approvalRequestId, ct);
        }

        public Task<long?> GetGatePassBranchIdAsync(long gatePassId, CancellationToken ct = default)
        {
            return _context.GatePasses
                .AsNoTracking()
                .Where(g => g.Id == gatePassId)
                .Join(
                    _context.ServiceOrders.AsNoTracking(),
                    g => g.ServiceOrderId,
                    so => so.Id,
                    (g, so) => (long?)so.ExecuteBranchId)
                .FirstOrDefaultAsync(ct);
        }

        public Task<long?> GetServiceOrderDiscountBranchIdAsync(long serviceOrderDiscountId, CancellationToken ct = default)
        {
            return _context.ServiceOrderDiscounts
                .AsNoTracking()
                .Where(d => d.Id == serviceOrderDiscountId)
                .Join(
                    _context.ServiceOrders.AsNoTracking(),
                    d => d.ServiceOrderId,
                    so => so.Id,
                    (d, so) => (long?)so.ExecuteBranchId)
                .FirstOrDefaultAsync(ct);
        }

        public Task<long?> GetMaterialReturnBranchIdAsync(long materialReturnId, CancellationToken ct = default)
        {
            return _context.MaterialReturns
                .AsNoTracking()
                .Where(m => m.Id == materialReturnId && m.ServiceOrderId.HasValue)
                .Join(
                    _context.ServiceOrders.AsNoTracking(),
                    m => m.ServiceOrderId!.Value,
                    so => so.Id,
                    (m, so) => (long?)so.ExecuteBranchId)
                .FirstOrDefaultAsync(ct);
        }

        public Task<long?> GetStaffBranchIdAsync(long staffId, CancellationToken ct = default)
        {
            return _context.Staffs
                .AsNoTracking()
                .Where(s => s.Id == staffId)
                .Select(s => (long?)s.BranchId)
                .FirstOrDefaultAsync(ct);
        }
    }
}
