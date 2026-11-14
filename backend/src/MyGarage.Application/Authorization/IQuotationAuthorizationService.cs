using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public sealed record UserActorContext(
    long? UserId,
    string? Role,
    string? RoleCode,
    long? BranchId,
    bool IsAdmin
);

public interface IQuotationAuthorizationService
{
    Task<bool> CanAccessQuotationAsync(long quotationId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanAccessQuotationDiscountsAsync(long quotationId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanUpdateQuotationDiscountsAsync(long quotationId, UserActorContext actor, CancellationToken cancellationToken = default);
}
