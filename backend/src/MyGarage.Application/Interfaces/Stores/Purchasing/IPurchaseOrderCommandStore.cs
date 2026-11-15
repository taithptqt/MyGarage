using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Purchasing;

public interface IPurchaseOrderCommandStore
{
    Task AddPurchaseOrderAndSaveAsync(PurchaseOrder po, CancellationToken ct = default);

    Task<PurchaseOrder?> GetPurchaseOrderWithItemsForUpdateAsync(long id, CancellationToken ct = default);

    Task RemovePurchaseOrderAndItemsAsync(PurchaseOrder po, CancellationToken ct = default);
}
