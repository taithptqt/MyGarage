using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IAdminPartsDataService
{
    Task<(bool Found, long Id, string Name)> GetActiveProductSummaryAsync(long productId, CancellationToken ct = default);
    Task SavePrimaryProductImageAsync(long productId, string imageUrl, CancellationToken ct = default);
    Task<ProductImage> AppendGalleryProductImageAsync(long productId, string imageUrl, CancellationToken ct = default);
}
