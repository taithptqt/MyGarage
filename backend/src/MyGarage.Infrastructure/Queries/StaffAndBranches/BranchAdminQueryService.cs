using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfBranchAdminQueryService : IBranchAdminQueryService
{
    private readonly IGarageContext _context;

    public EfBranchAdminQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<BranchAdminPageData> GetAdminPageAsync(string? search, int currentPage, int pageSize, CancellationToken ct = default)
    {
        var all = _context.Branches.AsNoTracking();
        var totalBranches = await all.CountAsync(ct);
        var activeBranches = await all.CountAsync(x => x.Status != null && x.Status.ToLower() == "active", ct);

        var filtered = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            filtered = filtered.Where(x =>
                (x.Name ?? "").ToLower().Contains(term) ||
                (x.Address ?? "").ToLower().Contains(term) ||
                (x.Hotline ?? "").ToLower().Contains(term) ||
                (x.Email ?? "").ToLower().Contains(term));
        }

        var total = await filtered.CountAsync(ct);
        var offset = ((long)currentPage - 1) * pageSize;
        var rows = offset >= total
            ? new List<Branch>()
            : await filtered.OrderBy(x => x.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .ToListAsync(ct);

        return new BranchAdminPageData(rows, total, totalBranches, activeBranches);
    }
}
