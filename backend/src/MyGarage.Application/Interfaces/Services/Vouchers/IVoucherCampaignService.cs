using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVoucherCampaignService : IGenericService<VoucherCampaign>
    {
        Task<IEnumerable<VoucherCampaign>> GetAllAsync(string? keyword = null, string? status = null, bool includeDeleted = false);
        Task<VoucherCampaign?> GetDetailedByIdAsync(long id, bool includeDeleted = false);
        Task<VoucherCampaign?> AssignVouchersAsync(long campaignId, IEnumerable<long> voucherIds, bool replaceExisting, long? updatedBy = null);
        Task<int> SyncLifecycleStatusesAsync(DateTime? now = null);
    }
}
