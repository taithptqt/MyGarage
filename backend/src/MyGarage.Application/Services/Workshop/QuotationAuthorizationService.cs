using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class QuotationAuthorizationService : IQuotationAuthorizationService
{
    private readonly IWorkshopAuthorizationStore _store;

    public QuotationAuthorizationService(IWorkshopAuthorizationStore store)
    {
        _store = store;
    }

    public async Task<bool> CanAccessQuotationAsync(long quotationId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (quotationId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        var meta = await _store.GetQuotationAuthMetaAsync(quotationId, cancellationToken);
        if (meta == null)
        {
            return false;
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return !meta.ExecuteBranchId.HasValue || actor.BranchId == meta.ExecuteBranchId.Value;
        }

        return meta.CustomerId.HasValue && actor.UserId.Value == meta.CustomerId.Value;
    }

    public async Task<bool> CanAccessQuotationDiscountsAsync(long quotationId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        return await CanAccessQuotationAsync(quotationId, actor, cancellationToken);
    }

    public async Task<bool> CanUpdateQuotationDiscountsAsync(long quotationId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (quotationId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isAdvisorOrReceptionist = IsAdvisorOrReceptionistRole(actor.RoleCode);
        if (!isAdvisorOrReceptionist)
        {
            return false;
        }

        var meta = await _store.GetQuotationAuthMetaAsync(quotationId, cancellationToken);
        if (meta == null)
        {
            return false;
        }

        return !meta.ExecuteBranchId.HasValue || actor.BranchId == meta.ExecuteBranchId.Value;
    }

    private static bool IsAdvisorOrReceptionistRole(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return false;
        }

        var normalized = roleCode.Trim().ToUpperInvariant();
        return normalized.Contains("SERVICE_ADVISOR", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("ADVISOR", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("CVDV", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("RECEPTIONIST", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("FRONT_DESK", StringComparison.OrdinalIgnoreCase);
    }
}
