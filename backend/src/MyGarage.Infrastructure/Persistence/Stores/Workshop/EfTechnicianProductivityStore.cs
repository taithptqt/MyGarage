using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed class EfTechnicianProductivityStore : ITechnicianProductivityStore
{
    private readonly IGarageContext _context;

    public EfTechnicianProductivityStore(IGarageContext context)
    {
        _context = context;
    }

    public Task<TechnicianProfileInfo?> GetTechnicianProfileAsync(long technicianId)
    {
        return _context.Staffs
            .AsNoTracking()
            .Where(x => x.Id == technicianId && x.IsDeleted != true)
            .Select(x => new TechnicianProfileInfo(x.Id, x.FullName, x.Specialty))
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<TechnicianProductivityQueryRow>> GetProductivityQueryRowsAsync(
        DateTime fromDateTime,
        DateTime toExclusive,
        long? technicianId,
        long? branchId)
    {
        var query = _context.ServiceStaffAssignments
            .AsNoTracking()
            .Where(x =>
                x.ActualStartTime.HasValue &&
                x.ActualEndTime.HasValue &&
                x.ActualEndTime.Value >= fromDateTime &&
                x.ActualEndTime.Value < toExclusive);

        if (technicianId.HasValue && technicianId.Value > 0)
        {
            query = query.Where(x => x.TechnicianId == technicianId.Value);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(x => x.ServiceOrderDetail.ServiceOrder.ExecuteBranchId == branchId.Value);
        }

        return await query
            .Select(x => new TechnicianProductivityQueryRow(
                x.Id,
                x.TechnicianId,
                x.Technician.FullName,
                x.Technician.Specialty,
                x.ServiceOrderDetailId,
                (long?)x.ServiceOrderDetail.ServiceOrderId,
                x.ServiceOrderDetail.ServiceOrder.Code,
                x.ServiceOrderDetail.StandardService != null
                    ? x.ServiceOrderDetail.StandardService.Name
                    : x.ServiceOrderDetail.ItemName,
                x.ActualStartTime!.Value,
                x.ActualEndTime!.Value,
                EF.Functions.DateDiffMinute(x.ActualStartTime!.Value, x.ActualEndTime!.Value),
                x.ServiceOrderDetail.EstimatedMinutes
                    ?? (x.ServiceOrderDetail.StandardService != null
                        ? (int?)x.ServiceOrderDetail.StandardService.StandardMinutes
                        : null)
                    ?? 0,
                x.SplitRatio,
                x.ServiceOrderDetail.FinalAmount))
            .ToListAsync();
    }
}
