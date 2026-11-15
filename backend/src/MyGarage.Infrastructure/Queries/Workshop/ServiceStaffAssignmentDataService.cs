using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfServiceStaffAssignmentDataService(IGarageContext context) : IServiceStaffAssignmentDataService
{
    public async Task<(IReadOnlyList<ServiceStaffAssignment> Items, int Total)> GetPagedAsync(
        long? scopedBranchId,
        long? serviceOrderId,
        long? staffId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.ServiceStaffAssignments.AsNoTracking().AsQueryable();
        if (scopedBranchId.HasValue)
        {
            query = query.Where(a => a.ServiceOrderDetail.ServiceOrder.ExecuteBranchId == scopedBranchId.Value);
        }

        if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
        {
            query = query.Where(a => a.ServiceOrderDetail.ServiceOrderId == serviceOrderId.Value);
        }

        if (staffId.HasValue && staffId.Value > 0)
        {
            query = query.Where(a => a.TechnicianId == staffId.Value);
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var paged = offset >= total
            ? new List<ServiceStaffAssignment>()
            : await query.OrderByDescending(a => a.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (paged, total);
    }

    public async Task AddAssignmentsAsync(IEnumerable<ServiceStaffAssignment> rows, CancellationToken ct = default)
    {
        context.ServiceStaffAssignments.AddRange(rows);
        await context.SaveChangesAsync(ct);
    }

    public Task<ServiceStaffAssignment?> GetAssignmentWithOrderAsync(long id, CancellationToken ct = default) =>
        context.ServiceStaffAssignments
            .Include(a => a.ServiceOrderDetail)
                .ThenInclude(d => d.ServiceOrder)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        context.SaveChangesAsync(ct);

    public Task<ServiceOrderDetailSummary?> GetDetailInfoAsync(long serviceOrderDetailId, CancellationToken ct = default) =>
        context.ServiceOrderDetails
            .AsNoTracking()
            .Where(x => x.Id == serviceOrderDetailId)
            .Select(x => new ServiceOrderDetailSummary(x.ItemName, x.Description, x.PayerType))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<long>> GetExistingAssignedTechnicianIdsAsync(
        long serviceOrderDetailId,
        IReadOnlyCollection<long> technicianIds,
        CancellationToken ct = default) =>
        await context.ServiceStaffAssignments
            .AsNoTracking()
            .Where(x => x.ServiceOrderDetailId == serviceOrderDetailId && technicianIds.Contains(x.TechnicianId))
            .Select(x => x.TechnicianId)
            .Distinct()
            .ToListAsync(ct);

    public Task<Dictionary<long, StaffTechnicianSummary>> GetTechniciansByIdsAsync(
        IReadOnlyCollection<long> technicianIds,
        CancellationToken ct = default) =>
        context.Staffs
            .AsNoTracking()
            .Where(x => technicianIds.Contains(x.Id) && x.IsDeleted != true)
            .Select(x => new StaffTechnicianSummary(x.Id, x.FullName, x.Specialty, x.BranchId))
            .ToDictionaryAsync(x => x.Id, x => x, ct);

    public Task<bool> AssignmentBelongsToBranchAsync(long assignmentId, long branchId, CancellationToken ct = default) =>
        context.ServiceStaffAssignments
            .AsNoTracking()
            .AnyAsync(a => a.Id == assignmentId && a.ServiceOrderDetail.ServiceOrder.ExecuteBranchId == branchId, ct);

    public Task<bool> ServiceOrderBelongsToBranchAsync(long serviceOrderId, long branchId, CancellationToken ct = default) =>
        context.ServiceOrders
            .AsNoTracking()
            .AnyAsync(so => so.Id == serviceOrderId && so.ExecuteBranchId == branchId, ct);

    public Task<long> GetServiceOrderIdFromDetailIdAsync(long serviceOrderDetailId, CancellationToken ct = default) =>
        context.ServiceOrderDetails
            .AsNoTracking()
            .Where(x => x.Id == serviceOrderDetailId)
            .Select(x => x.ServiceOrderId)
            .FirstOrDefaultAsync(ct);

    public Task<string?> GetDetailPayerTypeAsync(long serviceOrderDetailId, CancellationToken ct = default) =>
        context.ServiceOrderDetails
            .AsNoTracking()
            .Where(x => x.Id == serviceOrderDetailId)
            .Select(x => x.PayerType)
            .FirstOrDefaultAsync(ct);
}
