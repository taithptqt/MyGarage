using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IBayAuthorizationStore
    {
        Task<long?> GetBayBranchIdAsync(long bayId, CancellationToken cancellationToken = default);
        Task<long?> GetServiceOrderExecuteBranchIdAsync(long serviceOrderId, CancellationToken cancellationToken = default);
    }
}
