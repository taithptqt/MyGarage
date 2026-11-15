using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class SharedOperationsAuthorizationService : ISharedOperationsAuthorizationService
{
    private readonly ISharedOperationsAuthorizationStore _store;

    public SharedOperationsAuthorizationService(ISharedOperationsAuthorizationStore store)
    {
        _store = store;
    }

    private static bool IsAdminActor(UserActorContext actor)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (actor.IsAdmin || string.Equals(actor.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        return roleCode is "ADMIN" or "SUPERADMIN" or "SUPER_ADMIN";
    }

    private static bool IsStaffActor(UserActorContext actor)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        return IsAdminActor(actor) || string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasApproverRole(UserActorContext actor)
    {
        if (IsAdminActor(actor))
        {
            return true;
        }

        if (!IsStaffActor(actor))
        {
            return false;
        }

        var roleCode = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(roleCode))
        {
            return false;
        }

        return roleCode.Contains("ADMIN")
            || roleCode.Contains("MANAGER")
            || roleCode.Contains("FOREMAN")
            || roleCode.Contains("DIRECTOR")
            || roleCode.Contains("QUAN_LY");
    }

    public Task<bool> CanManageGatePassesAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        if (IsAdminActor(actor))
        {
            return Task.FromResult(true);
        }

        if (!IsStaffActor(actor))
        {
            return Task.FromResult(false);
        }

        if (!branchId.HasValue || branchId.Value <= 0)
        {
            return Task.FromResult(true);
        }

        if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(actor.BranchId.Value == branchId.Value);
    }

    public Task<bool> CanAccessApprovalRequestsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default)
    {
        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        if (IsAdminActor(actor))
        {
            return Task.FromResult(true);
        }

        if (!IsStaffActor(actor))
        {
            return Task.FromResult(false);
        }

        if (!branchId.HasValue || branchId.Value <= 0)
        {
            return Task.FromResult(true);
        }

        if (!actor.BranchId.HasValue || actor.BranchId.Value <= 0)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(actor.BranchId.Value == branchId.Value);
    }

    public async Task<bool> CanDecideApprovalRequestAsync(long approvalRequestId, UserActorContext actor, CancellationToken ct = default)
    {
        if (approvalRequestId <= 0 || !actor.UserId.HasValue || actor.UserId.Value <= 0)
        {
            return false;
        }

        if (IsAdminActor(actor))
        {
            return true;
        }

        if (!HasApproverRole(actor))
        {
            return false;
        }

        var request = await _store.GetApprovalRequestByIdAsync(approvalRequestId, ct);

        if (request == null)
        {
            return false;
        }

        if (actor.BranchId.HasValue && actor.BranchId.Value > 0)
        {
            long? targetBranchId = null;

            if (request.GatePassId.HasValue)
            {
                targetBranchId = await _store.GetGatePassBranchIdAsync(request.GatePassId.Value, ct);
            }
            else if (request.ServiceOrderDiscountId.HasValue)
            {
                targetBranchId = await _store.GetServiceOrderDiscountBranchIdAsync(request.ServiceOrderDiscountId.Value, ct);
            }
            else if (request.MaterialReturnId.HasValue)
            {
                targetBranchId = await _store.GetMaterialReturnBranchIdAsync(request.MaterialReturnId.Value, ct);
            }

            if (!targetBranchId.HasValue || targetBranchId.Value <= 0)
            {
                targetBranchId = await _store.GetStaffBranchIdAsync(request.RequestedBy, ct);
            }

            if (targetBranchId.HasValue && targetBranchId.Value > 0 && targetBranchId.Value != actor.BranchId.Value)
            {
                return false;
            }
        }

        return true;
    }

    public Task<bool> CanAccessAuditLogsAsync(UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(IsAdminActor(actor));
    }
}
