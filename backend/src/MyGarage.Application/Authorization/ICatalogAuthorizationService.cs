using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface ICatalogAuthorizationService
{
    Task<bool> CanManageCatalogAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewCatalogAdminListAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessCatalogItemAsync(UserActorContext actor, CancellationToken ct = default);
}
