using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Catalog.DTOs;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Application.Catalog;

public interface ICatalogStore
{
    Task<(IReadOnlyList<StandardServiceAdminItemDto> Items, int TotalCount)> GetStandardServiceAdminPageAsync(
        StandardServiceAdminPageQuery query,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<string?> GetActiveCategoryNameAsync(long categoryId, CancellationToken ct = default);

    Task<bool> ActiveCategoryExistsAsync(long categoryId, CancellationToken ct = default);

    Task<bool> StandardServiceCodeExistsAsync(string normalizedCode, CancellationToken ct = default);

    Task<StandardService?> GetStandardServiceByIdAsync(long id, bool track = false, CancellationToken ct = default);

    Task AddStandardServiceAsync(StandardService service, CancellationToken ct = default);

    Task SaveStandardServiceAsync(StandardService service, CancellationToken ct = default);

    Task<(IReadOnlyList<BrandItemDto> Items, int TotalCount)> GetBrandAdminPageAsync(
        BrandAdminPageQuery query,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<(IReadOnlyList<ModelItemDto> Items, int TotalCount)> GetModelAdminPageAsync(
        ModelAdminPageQuery query,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<(IReadOnlyList<CategoryItemDto> Items, int TotalCount)> GetCategoryAdminPageAsync(
        CategoryAdminPageQuery query,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<ServiceBomResponseDto>> GetServiceBomsAsync(
        long serviceId,
        long vehicleModelId,
        CancellationToken ct = default);
}
