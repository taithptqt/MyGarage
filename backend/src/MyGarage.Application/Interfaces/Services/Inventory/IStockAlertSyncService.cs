using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IStockAlertSyncService
    {
        Task<IReadOnlyList<object>> RebuildStockAlertsAsync(long? actorUserId = null);
        Task<bool> ResolveStockAlertAsync(long alertId, long purchaseOrderId, long? actorUserId = null);
    }
}
