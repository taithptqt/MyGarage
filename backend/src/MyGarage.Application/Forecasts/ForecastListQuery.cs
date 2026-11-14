namespace MyGarage.Application.Forecasts;

public sealed record ForecastRow(long Id, long VehicleId, string? Type, DateOnly PredictedDate,
    decimal? ConfidenceScore, DateTime? CreatedAt);

public interface IForecastListReader
{
    Task<(IReadOnlyList<ForecastRow> Items, int Total)> ReadAsync(int? page, int? pageSize, CancellationToken cancellationToken);
}

public sealed class ForecastListQuery(IForecastListReader reader)
{
    public Task<(IReadOnlyList<ForecastRow> Items, int Total)> ExecuteAsync(int? page, int? pageSize, CancellationToken cancellationToken)
        => reader.ReadAsync(page, pageSize, cancellationToken);
}
