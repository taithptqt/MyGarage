using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfCommissionTransactionQueryService(IGarageContext context) : ICommissionTransactionQueryService
{
    public async Task<IReadOnlyList<CommissionTransaction>> GetCommissionsAsync(
        long? scopedBranchId,
        long? staffId,
        string? status,
        DateTime? monthStart,
        DateTime? monthEndExclusive,
        CancellationToken ct = default)
    {
        var query = context.CommissionTransactions
            .AsNoTracking()
            .Include(ct => ct.Staff)
            .Include(ct => ct.ServiceOrder)
            .AsQueryable();

        if (scopedBranchId.HasValue)
        {
            query = query.Where(ct =>
                ct.Staff.BranchId == scopedBranchId.Value ||
                (ct.ServiceOrder != null && ct.ServiceOrder.ExecuteBranchId == scopedBranchId.Value));
        }

        if (staffId.HasValue && staffId.Value > 0)
        {
            query = query.Where(ct => ct.StaffId == staffId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(ct => ct.Status != null && ct.Status.ToUpper() == normalizedStatus);
        }

        if (monthStart.HasValue && monthEndExclusive.HasValue)
        {
            query = query.Where(ct =>
                (ct.CreatedAt ?? ct.ApprovedAt) >= monthStart.Value &&
                (ct.CreatedAt ?? ct.ApprovedAt) < monthEndExclusive.Value);
        }

        return await query
            .OrderByDescending(ct => ct.CreatedAt ?? ct.ApprovedAt)
            .ThenByDescending(ct => ct.Id)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<CommissionTransaction> Items, int Total)> GetPageAsync(
        long? scopedBranchId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.CommissionTransactions.AsNoTracking().AsQueryable();
        if (scopedBranchId.HasValue)
        {
            query = query.Where(ct => ct.Staff.BranchId == scopedBranchId.Value);
        }

        var ordered = query.OrderByDescending(ct => ct.Id);
        var total = await ordered.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? []
            : await ordered.Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (items, total);
    }

    public Task<CommissionTransaction?> GetByIdWithStaffAsync(long id, CancellationToken ct = default)
        => context.CommissionTransactions
            .AsNoTracking()
            .Include(ct => ct.Staff)
            .FirstOrDefaultAsync(ct => ct.Id == id, ct);

    public Task<long?> GetStaffBranchIdAsync(long staffId, CancellationToken ct = default)
        => context.Staffs
            .AsNoTracking()
            .Where(s => s.Id == staffId)
            .Select(s => (long?)s.BranchId)
            .FirstOrDefaultAsync(ct);
}
