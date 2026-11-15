using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IMaterialRequestDetailQueryService
{
    Task<IReadOnlyList<MaterialRequestDetail>> GetIssuedByServiceOrderAsync(long serviceOrderId, CancellationToken ct = default);
    Task<long> ResolveServiceOrderIdFromRequestIdAsync(long requestId, CancellationToken ct = default);
}
