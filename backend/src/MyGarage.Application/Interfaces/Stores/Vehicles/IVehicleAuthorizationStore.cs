using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public interface IVehicleAuthorizationStore
{
    Task<bool> CustomerHasVehicleAccessAsync(long vehicleId, long customerId, CancellationToken ct = default);
    Task<bool> CustomerIsCurrentOwnerAsync(long vehicleId, long customerId, CancellationToken ct = default);
    Task<bool> CustomerOwnsCustomerVehicleAsync(long customerVehicleId, long customerId, CancellationToken ct = default);
}
