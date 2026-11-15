using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVoucherRedemptionRepository : IGenericRepository<VoucherRedemption>
    {
        Task<IEnumerable<VoucherRedemption>> GetAllAsync(string? status = null);
        Task<VoucherRedemption?> GetDetailedByIdAsync(long id);
        Task<VoucherRedemption?> GetByIdempotencyKeyAsync(string idempotencyKey);
    }
}

