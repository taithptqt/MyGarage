namespace MyGarage.Application.Ai;

public sealed record DispatchRow(long Id, long BranchId, DateOnly Date, string? Recommendation,
    decimal? EfficiencyScore, string? Status, DateTime? CreatedAt);

public interface IDispatchListReader
{
    Task<(IReadOnlyList<DispatchRow> Items, int Total)> ReadAsync(int? page, int? pageSize, CancellationToken cancellationToken);
}

public sealed class DispatchListQuery(IDispatchListReader reader)
{
    public Task<(IReadOnlyList<DispatchRow> Items, int Total)> ExecuteAsync(int? page, int? pageSize, CancellationToken cancellationToken)
        => reader.ReadAsync(page, pageSize, cancellationToken);
}
