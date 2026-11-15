using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public sealed record QuotationAuthMeta(long? CustomerId, long? ExecuteBranchId);

public sealed record ServiceOrderAuthMeta(long CustomerId, long ExecuteBranchId, long OriginBranchId);

public interface IWorkshopAuthorizationStore
{
    Task<QuotationAuthMeta?> GetQuotationAuthMetaAsync(long quotationId, CancellationToken ct = default);
    Task<ServiceOrderAuthMeta?> GetServiceOrderAuthMetaAsync(long serviceOrderId, CancellationToken ct = default);
    Task<bool> IsTechnicianAssignedToDetailAsync(long serviceOrderDetailId, long technicianId, CancellationToken ct = default);
    Task<bool> DidTechnicianWorkOnOrderAsync(long serviceOrderId, long technicianId, CancellationToken ct = default);
    Task<(bool Found, long? BranchId)> GetEditRequestBranchIdAsync(long requestId, CancellationToken ct = default);
}
