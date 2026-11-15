using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed class EfWorkshopDispatchStore : IWorkshopDispatchStore
{
    private readonly IGarageContext _context;

    public EfWorkshopDispatchStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServiceOrder>> GetDispatchServiceOrdersAsync(
        long branchId,
        DateTime dayStart,
        DateTime dayEnd,
        IReadOnlyCollection<string> activeOrderStatuses)
    {
        return await _context.ServiceOrders.AsNoTracking()
            .Include(x => x.Vehicle)
            .Include(x => x.Customer)
            .Where(x => x.ExecuteBranchId == branchId)
            .Where(x =>
                activeOrderStatuses.Contains(x.Status) ||
                (x.CheckInTime.HasValue && x.CheckInTime.Value >= dayStart && x.CheckInTime.Value < dayEnd))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
            .Take(200)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<BayAllocation>> GetBayAllocationsByServiceOrderIdsAsync(IReadOnlyCollection<long> serviceOrderIds)
    {
        return await _context.BayAllocations.AsNoTracking()
            .Include(x => x.Bay)
            .Where(x => serviceOrderIds.Contains(x.ServiceOrderId))
            .OrderByDescending(x => x.AllocatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceOrderDetail>> GetServiceOrderDetailsByServiceOrderIdsAsync(IReadOnlyCollection<long> serviceOrderIds)
    {
        return await _context.ServiceOrderDetails.AsNoTracking()
            .Where(x => serviceOrderIds.Contains(x.ServiceOrderId))
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceStaffAssignment>> GetAssignmentsByDetailIdsAsync(IReadOnlyCollection<long> detailIds)
    {
        return await _context.ServiceStaffAssignments.AsNoTracking()
            .Where(x => detailIds.Contains(x.ServiceOrderDetailId))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
            .ToListAsync();
    }

    public async Task<Dictionary<long, Staff>> GetTechniciansByIdsAsync(IReadOnlyCollection<long> technicianIds)
    {
        return await _context.Staffs.AsNoTracking()
            .Where(x => technicianIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);
    }

    public async Task<IReadOnlyList<MaterialRequest>> GetMaterialRequestsByServiceOrderIdsAsync(IReadOnlyCollection<long> serviceOrderIds)
    {
        return await _context.MaterialRequests.AsNoTracking()
            .Where(x => x.ServiceOrderId.HasValue && serviceOrderIds.Contains(x.ServiceOrderId.Value))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MaterialRequestDetail>> GetMaterialRequestDetailsByRequestIdsAsync(IReadOnlyCollection<long> materialRequestIds)
    {
        return await _context.MaterialRequestDetails.AsNoTracking()
            .Where(x => materialRequestIds.Contains(x.RequestId))
            .ToListAsync();
    }
}
