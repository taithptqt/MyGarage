namespace MyGarage.Application.Catalog;

public sealed record StandardServicePageItem(long Id, string? Code, string? Name, long CategoryId,
    decimal? BasePrice, string? EngineType, int StandardMinutes, decimal? TaxRate, int? WarrantyMonths,
    int? WarrantyKm, string? Description, string? ImageUrl, decimal? ConsumableCost, bool? IsActive,
    DateTime? CreatedAt, long? CreatedBy, DateTime? UpdatedAt, long? UpdatedBy,
    bool? IsDeleted, DateTime? DeletedAt, long? DeletedBy);
public sealed record StandardServicePage(IReadOnlyList<StandardServicePageItem> Items, int Total,
    int TotalServices, int ActiveServices, IReadOnlyDictionary<long, string> CategoryNames);
public interface IStandardServicePageReader
{
    Task<StandardServicePage> ReadAsync(string? search, bool nameCodeOnly, int page, int pageSize, CancellationToken cancellationToken);
}
public sealed class StandardServicePageQuery(IStandardServicePageReader reader)
{
    public Task<StandardServicePage> ExecuteAsync(string? search, int? page, int? pageSize, bool nameCodeOnly, CancellationToken cancellationToken)
    {
        var size = pageSize.GetValueOrDefault(10);
        if (size <= 0) size = 10;
        return reader.ReadAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant(),
            nameCodeOnly, Math.Max(1, page.GetValueOrDefault(1)), Math.Clamp(size, 1, 100), cancellationToken);
    }
}
