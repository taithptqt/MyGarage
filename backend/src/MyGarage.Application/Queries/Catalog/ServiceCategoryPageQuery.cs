namespace MyGarage.Application.Catalog;

public sealed record ServiceCategoryPageItem(long Id, string? Code, string? Name, long? ParentCategoryId,
    string? Description, bool? IsActive, int? DisplayOrder, DateTime? CreatedAt, long? CreatedBy,
    DateTime? UpdatedAt, long? UpdatedBy, bool? IsDeleted, DateTime? DeletedAt, long? DeletedBy);

public sealed record ServiceCategoryPage(IReadOnlyList<ServiceCategoryPageItem> Items, int Total);

public interface IServiceCategoryPageReader
{
    Task<ServiceCategoryPage> ReadAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class ServiceCategoryPageQuery(IServiceCategoryPageReader reader)
{
    public Task<ServiceCategoryPage> ExecuteAsync(string? search, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var size = pageSize.GetValueOrDefault(10);
        if (size <= 0) size = 10;
        return reader.ReadAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant(),
            Math.Max(1, page.GetValueOrDefault(1)), Math.Clamp(size, 1, 100), cancellationToken);
    }
}
