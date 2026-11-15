using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class VoucherAuthorizationService : IVoucherAuthorizationService
{
    private readonly IGarageContext _context;

    public VoucherAuthorizationService(IGarageContext context)
    {
        _context = context;
    }

    private static bool IsStaff(UserActorContext actor)
    {
        if (!actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;
        return string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdmin(UserActorContext actor)
    {
        if (!actor.UserId.HasValue) return false;
        if (actor.IsAdmin) return true;
        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase)) return false;
        var code = (actor.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        return code is "ADMIN" or "SUPERADMIN" or "SUPER_ADMIN";
    }

    public Task<bool> CanViewVouchersAsync(UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(actor.UserId.HasValue);
    }

    public Task<bool> CanViewVoucherAsync(long voucherId, UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(voucherId > 0 && actor.UserId.HasValue);
    }

    public Task<bool> CanCreateVoucherAsync(UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(IsStaff(actor));
    }

    public Task<bool> CanUpdateVoucherAsync(long voucherId, UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(voucherId > 0 && IsStaff(actor));
    }

    public Task<bool> CanPatchVoucherStatusAsync(long voucherId, UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(voucherId > 0 && IsStaff(actor));
    }

    public Task<bool> CanDeleteVoucherAsync(long voucherId, UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(voucherId > 0 && IsStaff(actor));
    }

    public Task<bool> CanValidateVoucherAsync(UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(actor.UserId.HasValue);
    }

    public Task<bool> CanRedeemVoucherAsync(UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(actor.UserId.HasValue);
    }

    public Task<bool> CanReverseRedemptionAsync(long redemptionId, UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(redemptionId > 0 && IsStaff(actor));
    }

    public Task<bool> CanManageCampaignsAsync(UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(IsStaff(actor));
    }

    public Task<bool> CanDecideApprovalRequestAsync(long requestId, UserActorContext actor, CancellationToken ct = default)
    {
        return Task.FromResult(requestId > 0 && IsAdmin(actor));
    }
}
