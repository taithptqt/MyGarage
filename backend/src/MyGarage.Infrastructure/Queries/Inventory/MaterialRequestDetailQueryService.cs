using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfMaterialRequestDetailQueryService(IGarageContext context) : IMaterialRequestDetailQueryService
{
    public async Task<IReadOnlyList<MaterialRequestDetail>> GetIssuedByServiceOrderAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return await context.MaterialRequestDetails
            .Include(d => d.Product)
            .Where(d => d.Request.ServiceOrderId == serviceOrderId && (d.IssuedQuantity ?? 0) > 0)
            .ToListAsync(ct);
    }

    public async Task<long> ResolveServiceOrderIdFromRequestIdAsync(long requestId, CancellationToken ct = default)
    {
        if (requestId <= 0)
        {
            return 0;
        }

        var serviceOrderId = await context.MaterialRequests
            .AsNoTracking()
            .Where(request => request.Id == requestId)
            .Select(request => request.ServiceOrderId)
            .FirstOrDefaultAsync(ct);

        return !serviceOrderId.HasValue || serviceOrderId.Value <= 0 ? 0 : serviceOrderId.Value;
    }
}
