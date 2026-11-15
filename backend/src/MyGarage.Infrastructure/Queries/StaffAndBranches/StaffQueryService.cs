using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfStaffQueryService : IStaffQueryService
{
    private readonly IGarageContext _context;

    public EfStaffQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Staff> Items, int Total)> GetPagedAsync(
        long? branchId,
        long? teamId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Staffs.AsNoTracking().AsQueryable();
        if (branchId.HasValue)
        {
            query = query.Where(s => s.BranchId == branchId.Value);
        }

        if (teamId.HasValue)
        {
            query = query.Where(s => s.TeamId == teamId.Value);
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? new List<Staff>()
            : await query.OrderBy(s => s.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (items, total);
    }

    public async Task<StaffAdminListData> GetAdminListAsync(
        long? branchId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var baseQuery = _context.Staffs.AsNoTracking().AsQueryable();
        if (branchId.HasValue)
        {
            baseQuery = baseQuery.Where(s => s.BranchId == branchId.Value);
        }

        var totalStaff = await baseQuery.CountAsync(ct);
        var activeStaff = await baseQuery.CountAsync(s => s.Status != null && s.Status.ToLower() == "active", ct);
        var rolesUsed = await baseQuery.Where(s => s.RoleId.HasValue && s.RoleId != 0)
            .Select(s => s.RoleId)
            .Distinct()
            .CountAsync(ct);

        var query = baseQuery;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(s =>
                (s.FullName ?? "").ToLower().Contains(term) ||
                (s.Email ?? "").ToLower().Contains(term) ||
                (s.Phone ?? "").ToLower().Contains(term) ||
                (s.Role != null && ((s.Role.RoleName != null && s.Role.RoleName != "" ? s.Role.RoleName : s.Role.RoleCode) ?? "").ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? new List<Staff>()
            : await query.OrderBy(s => s.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return new StaffAdminListData(items, total, totalStaff, activeStaff, rolesUsed);
    }
}
