using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVoucherApprovalRequestRepository : IGenericRepository<VoucherApprovalRequest>
    {
        Task<IEnumerable<VoucherApprovalRequest>> GetAllAsync(string? status = null);
        Task<VoucherApprovalRequest?> GetDetailedByIdAsync(long id);
    }
}

