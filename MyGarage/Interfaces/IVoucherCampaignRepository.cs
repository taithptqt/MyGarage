using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVoucherCampaignRepository : IGenericRepository<VoucherCampaign>
    {
        Task<IEnumerable<VoucherCampaign>> GetAllAsync(string? keyword = null, string? status = null, bool includeDeleted = false);
        Task<VoucherCampaign?> GetDetailedByIdAsync(long id, bool includeDeleted = false);
        Task<bool> CampaignCodeExistsAsync(string campaignCode, long? excludeId = null);
    }
}
