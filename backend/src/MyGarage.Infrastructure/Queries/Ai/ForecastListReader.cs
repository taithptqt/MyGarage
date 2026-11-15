using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Forecasts;
using MyGarage.Data;

namespace MyGarage.Infrastructure.Ai;

public sealed class ForecastListReader(GarageContext db) : IForecastListReader
{
    public async Task<(IReadOnlyList<ForecastRow> Items, int Total)> ReadAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var source = db.AiForecasts.AsNoTracking();
        IQueryable<ForecastRow> query = source.OrderBy(x => x.Id).Select(x => new ForecastRow(x.Id, x.VehicleId,
            x.Type, x.PredictedDate, x.ConfidenceScore, x.CreatedAt));
        if (!page.HasValue || !pageSize.HasValue)
        {
            var all = await query.ToListAsync(cancellationToken);
            return (all, all.Count);
        }
        var total = await source.CountAsync(cancellationToken);
        var offset = ((long)page.Value - 1) * pageSize.Value;
        if (offset >= total) return (Array.Empty<ForecastRow>(), total);
        query = query.Skip((int)offset).Take(pageSize.Value);
        return (await query.ToListAsync(cancellationToken), total);
    }
}
