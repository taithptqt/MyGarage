using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Catalog.DTOs;

namespace MyGarage.Application.Catalog;

public interface ICatalogApplicationService
{
    Task<StandardServiceAdminPageResult> GetStandardServiceAdminPageAsync(StandardServiceAdminPageQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<StandardServiceAdminItemDto> CreateStandardServiceAsync(StandardServiceCreateCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<StandardServiceAdminItemDto?> UpdateStandardServiceAsync(long id, StandardServiceUpdateCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<BrandAdminPageResult> GetBrandAdminPageAsync(BrandAdminPageQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<ModelAdminPageResult> GetModelAdminPageAsync(ModelAdminPageQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<CategoryAdminPageResult> GetCategoryAdminPageAsync(CategoryAdminPageQuery query, UserActorContext actor, CancellationToken ct = default);
}
