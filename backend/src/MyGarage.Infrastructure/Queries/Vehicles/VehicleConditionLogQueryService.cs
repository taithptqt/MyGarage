using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfVehicleConditionLogQueryService(IGarageContext context) : IVehicleConditionLogQueryService
{
    public async Task<IReadOnlyList<InspectionServiceRow>> GetInspectionServiceRowsAsync(
        IReadOnlyCollection<long> requestedServiceIds,
        CancellationToken ct = default)
    {
        if (requestedServiceIds.Count == 0)
        {
            return [];
        }

        return await context.StandardServices
            .AsNoTracking()
            .Where(x => requestedServiceIds.Contains(x.Id))
            .Join(
                context.ServiceCategories.AsNoTracking(),
                service => service.CategoryId,
                category => category.Id,
                (service, category) => new InspectionServiceRow(
                    service.Code,
                    service.Name,
                    category.Code,
                    category.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ServiceOrderDetailTextRow>> GetServiceOrderDetailTextsAsync(
        long serviceOrderId,
        CancellationToken ct = default)
    {
        return await context.ServiceOrderDetails
            .AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Select(x => new ServiceOrderDetailTextRow(x.ItemName, x.Description))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<long>> LoadRequestedServiceIdsFromServiceOrderAsync(
        long serviceOrderId,
        CancellationToken ct = default)
    {
        return await context.ServiceOrderDetails
            .AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Where(x => x.StandardServiceId.HasValue && x.StandardServiceId.Value > 0)
            .Select(x => x.StandardServiceId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<string?> GetAppointmentRequestedServiceIdsAsync(long appointmentId, CancellationToken ct = default)
        => context.Appointments
            .AsNoTracking()
            .Where(x => x.Id == appointmentId)
            .Select(x => x.RequestedServiceIds)
            .FirstOrDefaultAsync(ct);
}
