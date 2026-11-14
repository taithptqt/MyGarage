using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IVoucherAuthorizationService
{
    Task<bool> CanViewVouchersAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewVoucherAsync(long voucherId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanCreateVoucherAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanUpdateVoucherAsync(long voucherId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanPatchVoucherStatusAsync(long voucherId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanDeleteVoucherAsync(long voucherId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanValidateVoucherAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanRedeemVoucherAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanReverseRedemptionAsync(long redemptionId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanManageCampaignsAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanDecideApprovalRequestAsync(long requestId, UserActorContext actor, CancellationToken ct = default);
}
