using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class CatalogAuthorizationService : ICatalogAuthorizationService
{
    private readonly IGarageContext _context;

    public CatalogAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    public Task<bool> CanAccessCatalogItemAsync(UserActorContext actor, CancellationToken ct = default)
    {
        // Public / all authenticated users can view catalog items
        return Task.FromResult(true);
    }

    public Task<bool> CanViewCatalogAdminListAsync(UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(actor.IsAdmin || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase));
    }

    public Task<bool> CanManageCatalogAsync(UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        if (actor.IsAdmin)
        {
            return Task.FromResult(true);
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(string.Equals(actor.RoleCode, "ADMIN", StringComparison.OrdinalIgnoreCase) || actor.IsAdmin);
        }

        return Task.FromResult(false);
    }
}
