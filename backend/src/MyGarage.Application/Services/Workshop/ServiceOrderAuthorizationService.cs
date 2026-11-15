using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class ServiceOrderAuthorizationService : IServiceOrderAuthorizationService
{
    private readonly IWorkshopAuthorizationStore _store;

    public ServiceOrderAuthorizationService(IWorkshopAuthorizationStore store)
    {
        _store = store;
    }

    public async Task<bool> CanAccessServiceOrderAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        var meta = await _store.GetServiceOrderAuthMetaAsync(serviceOrderId, cancellationToken);
        if (meta == null)
        {
            return false;
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return !actor.BranchId.HasValue ||
                   actor.BranchId.Value == meta.ExecuteBranchId ||
                   actor.BranchId.Value == meta.OriginBranchId;
        }

        return actor.UserId.Value == meta.CustomerId;
    }

    public async Task<bool> CanMutateWorkshopAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || !actor.UserId.HasValue)
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

        var meta = await _store.GetServiceOrderAuthMetaAsync(serviceOrderId, cancellationToken);
        if (meta == null)
        {
            return false;
        }

        return !actor.BranchId.HasValue || actor.BranchId.Value == meta.ExecuteBranchId;
    }

    public async Task<bool> CanDispatchOrderAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (!await CanMutateWorkshopAsync(serviceOrderId, actor, cancellationToken))
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        return IsDispatcherOrForemanRole(actor.RoleCode);
    }

    public async Task<bool> CanLogTechnicianWorkAsync(long serviceOrderId, long serviceOrderDetailId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || serviceOrderDetailId <= 0 || !actor.UserId.HasValue)
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

        var meta = await _store.GetServiceOrderAuthMetaAsync(serviceOrderId, cancellationToken);
        if (meta == null || (actor.BranchId.HasValue && actor.BranchId.Value != meta.ExecuteBranchId))
        {
            return false;
        }

        if (IsDispatcherOrForemanRole(actor.RoleCode))
        {
            return true;
        }

        return await _store.IsTechnicianAssignedToDetailAsync(serviceOrderDetailId, actor.UserId.Value, cancellationToken);
    }

    public async Task<bool> CanPerformQcInspectionAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var order = await _store.GetServiceOrderAuthMetaAsync(serviceOrderId, cancellationToken);
        if (order == null)
        {
            return false;
        }

        if (!actor.IsAdmin && actor.BranchId.HasValue && actor.BranchId.Value != order.ExecuteBranchId)
        {
            return false;
        }

        // Separation of Duties: Inspector cannot be a technician who performed work on this order
        var workedOnOrder = await _store.DidTechnicianWorkOnOrderAsync(serviceOrderId, actor.UserId.Value, cancellationToken);
        if (workedOnOrder)
        {
            return false; // Self-inspection forbidden
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        return IsQcOrForemanRole(actor.RoleCode);
    }

    public async Task<bool> CanPerformHandoverAsync(long serviceOrderId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (serviceOrderId <= 0 || !actor.UserId.HasValue)
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

        var order = await _store.GetServiceOrderAuthMetaAsync(serviceOrderId, cancellationToken);
        if (order == null)
        {
            return false;
        }

        if (actor.BranchId.HasValue && actor.BranchId.Value != order.ExecuteBranchId)
        {
            return false;
        }

        return IsHandoverRole(actor.RoleCode);
    }

    public async Task<bool> CanDecideEditRequestAsync(long requestId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (requestId <= 0 || !actor.UserId.HasValue)
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

        var (found, branchId) = await _store.GetEditRequestBranchIdAsync(requestId, cancellationToken);
        if (!found)
        {
            return false;
        }

        return !actor.BranchId.HasValue || branchId == actor.BranchId.Value;
    }

    private static bool IsDispatcherOrForemanRole(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return true;
        var normalized = roleCode.Trim().ToUpperInvariant();
        return normalized.Contains("DISPATCH") ||
               normalized.Contains("FOREMAN") ||
               normalized.Contains("TEAM_LEAD") ||
               normalized.Contains("QUAN_DOC") ||
               normalized.Contains("MANAGER") ||
               normalized.Contains("ADMIN") ||
               normalized.Contains("ADVISOR") ||
               normalized.Contains("CVDV");
    }

    private static bool IsQcOrForemanRole(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return true;
        var normalized = roleCode.Trim().ToUpperInvariant();
        return normalized.Contains("QC") ||
               normalized.Contains("INSPECTOR") ||
               normalized.Contains("FOREMAN") ||
               normalized.Contains("TEAM_LEAD") ||
               normalized.Contains("QUAN_DOC") ||
               normalized.Contains("MANAGER") ||
               normalized.Contains("ADMIN");
    }

    private static bool IsHandoverRole(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode)) return true;
        var normalized = roleCode.Trim().ToUpperInvariant();
        return normalized.Contains("ADVISOR") ||
               normalized.Contains("CVDV") ||
               normalized.Contains("RECEPTIONIST") ||
               normalized.Contains("FRONT_DESK") ||
               normalized.Contains("CASHIER") ||
               normalized.Contains("THU_NGAN") ||
               normalized.Contains("MANAGER") ||
               normalized.Contains("ADMIN");
    }
}
