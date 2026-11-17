using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IChatCallerCustomerResolver
    {
        Task<long> ResolveCustomerIdAsync(long callerUserId, string? role, CancellationToken ct = default);
    }
}
