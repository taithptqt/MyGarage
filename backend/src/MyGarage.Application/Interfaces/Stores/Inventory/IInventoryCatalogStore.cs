using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Inventory.DTOs;
using MyGarage.Models;

namespace MyGarage.Application.Inventory;

public interface IInventoryCatalogStore
{
    Task<CatalogSummaryDto> GetCatalogSummaryAsync(CancellationToken ct = default);
    Task<CatalogProductsPageData> GetCatalogProductsPageAsync(int page, int pageSize, CancellationToken ct = default);
    Task<CatalogCategoriesPageData> GetCatalogCategoriesPageAsync(int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<Supplier>> GetCatalogSuppliersPageAsync(int page, int pageSize, CancellationToken ct = default);
    IAsyncEnumerable<ProductCsvRow> StreamProductsForCsvAsync(CancellationToken ct = default);
    Task<PartsPageData> GetPartsPageDataAsync(string? search, int page, int pageSize, CancellationToken ct = default);
}
