using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherCampaignService
    {
        public async Task<IEnumerable<VoucherCampaign>> GetAllAsync(string? keyword = null, string? status = null, bool includeDeleted = false)
        {
            await SyncLifecycleStatusesAsync();
            return await _voucherCampaignRepository.GetAllAsync(keyword, status, includeDeleted);
        }

        public async Task<VoucherCampaign?> GetDetailedByIdAsync(long id, bool includeDeleted = false)
        {
            await SyncLifecycleStatusesAsync();
            return await _voucherCampaignRepository.GetDetailedByIdAsync(id, includeDeleted);
        }


    }
}
