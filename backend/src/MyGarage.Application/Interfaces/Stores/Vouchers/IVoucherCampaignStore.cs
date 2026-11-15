using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Vouchers;

public interface IVoucherCampaignStore
{
    Task<VoucherCampaign?> GetCampaignByIdAsync(long id, bool track = false, bool excludeDeleted = false, CancellationToken ct = default);

    Task<bool> DeleteCampaignUnassigningVouchersAsync(long campaignId, DateTime now, CancellationToken ct = default);

    Task<IReadOnlyList<VoucherCampaign>> GetActiveLifecycleCampaignsAsync(CancellationToken ct = default);

    Task<IReadOnlyDictionary<long, decimal>> GetVoucherBudgetUsedByCampaignAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Voucher>> GetVouchersToUnassignFromCampaignAsync(long campaignId, IReadOnlyCollection<long> keepVoucherIds, CancellationToken ct = default);

    Task<IReadOnlyList<Voucher>> GetNonDeletedVouchersByIdsAsync(IReadOnlyCollection<long> voucherIds, CancellationToken ct = default);

    Task<decimal> GetCampaignVoucherBudgetUsedAsync(long campaignId, CancellationToken ct = default);
}
