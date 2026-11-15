using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Ai;
using MyGarage.Data;

namespace MyGarage.Infrastructure.Ai;

public sealed class DispatchListReader(GarageContext db) : IDispatchListReader
{
    public async Task<(IReadOnlyList<DispatchRow> Items, int Total)> ReadAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var source = db.AiSmartDispatchings.AsNoTracking();
        IQueryable<DispatchRow> query = source.OrderBy(x => x.Id).Select(x => new DispatchRow(x.Id, x.BranchId,
            x.Date, x.Recommendation, x.EfficiencyScore, x.Status, x.CreatedAt));
        if (!page.HasValue || !pageSize.HasValue)
        {
            var all = await query.ToListAsync(cancellationToken);
            return (all, all.Count);
        }
        var total = await source.CountAsync(cancellationToken);
        var offset = ((long)page.Value - 1) * pageSize.Value;
        if (offset >= total) return (Array.Empty<DispatchRow>(), total);
        query = query.Skip((int)offset).Take(pageSize.Value);
        return (await query.ToListAsync(cancellationToken), total);
    }
}
