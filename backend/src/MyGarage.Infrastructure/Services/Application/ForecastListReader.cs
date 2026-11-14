using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Forecasts;
using MyGarage.Interfaces;

namespace MyGarage.Services;

// Transitional adapter: the legacy context still owns the forecast table.
public sealed class ForecastListReader(IGarageContext db) : IForecastListReader
{
    public async Task<(IReadOnlyList<ForecastRow> Items, int Total)> ReadAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var query = db.AiForecasts.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        IQueryable<ForecastRow> rows = query.OrderBy(x => x.Id).Select(x => new ForecastRow(x.Id, x.VehicleId, x.Type,
            x.PredictedDate, x.ConfidenceScore, x.CreatedAt));
        if (page.HasValue && pageSize.HasValue)
        {
            var offset = ((long)page.Value - 1) * pageSize.Value;
            if (offset >= total) return (Array.Empty<ForecastRow>(), total);
            rows = rows.Skip((int)offset).Take(pageSize.Value);
        }
        return (await rows.ToListAsync(cancellationToken), total);
    }
}
