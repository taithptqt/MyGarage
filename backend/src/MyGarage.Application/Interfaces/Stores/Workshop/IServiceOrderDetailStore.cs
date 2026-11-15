using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public sealed record SupplementaryQuotationMeta(
    long QuotationId,
    long ServiceOrderId,
    int VersionNo,
    DateTime? CreatedAt,
    long? CreatedBy);

public interface IServiceOrderDetailStore
{
    Task<IReadOnlyList<ServiceOrderDetail>> GetByServiceOrderIdAsync(long serviceOrderId, CancellationToken ct = default);
    Task<ServiceOrder?> GetServiceOrderForUpdateAsync(long serviceOrderId, CancellationToken ct = default);
    Task<bool> HasWaitingPartsDetailAsync(long serviceOrderId, CancellationToken ct = default);
    Task<long> GetServiceOrderIdByDetailIdAsync(long detailId, CancellationToken ct = default);
    Task<bool> ActiveInsuranceCompanyExistsAsync(long insuranceCompanyId, CancellationToken ct = default);
    Task<long?> GetQuotationIdByDetailIdAsync(long quotationDetailId, CancellationToken ct = default);
    Task<SupplementaryQuotationMeta?> GetSupplementaryQuotationMetaAsync(long quotationDetailId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
