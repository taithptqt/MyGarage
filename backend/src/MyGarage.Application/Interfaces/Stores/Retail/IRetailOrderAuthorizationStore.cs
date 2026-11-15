using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Retail;

public sealed record RetailOrderAuthInfo(long BranchId, long? CustomerId, string Status);

public interface IRetailOrderAuthorizationStore
{
    Task<RetailOrderAuthInfo?> GetRetailOrderAuthInfoAsync(long retailOrderId, CancellationToken ct = default);
    Task<long?> GetRetailReturnBranchIdAsync(long retailReturnId, CancellationToken ct = default);
}
