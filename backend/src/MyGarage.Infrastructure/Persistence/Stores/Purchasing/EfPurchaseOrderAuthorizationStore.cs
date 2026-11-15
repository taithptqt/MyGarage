using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Purchasing;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.Purchasing;

public sealed class EfPurchaseOrderAuthorizationStore : IPurchaseOrderAuthorizationStore
{
    private readonly IGarageContext _context;

    public EfPurchaseOrderAuthorizationStore(IGarageContext context)
    {
        _context = context;
    }

    public Task<long?> GetPurchaseOrderBranchIdAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        return _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Id == purchaseOrderId)
            .Select(po => (long?)po.BranchId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<long?> GetGoodsReceiptBranchIdAsync(long goodsReceiptId, CancellationToken ct = default)
    {
        var grn = await _context.GoodsReceiptNotes
            .AsNoTracking()
            .Where(g => g.Id == goodsReceiptId)
            .Select(g => new
            {
                WarehouseBranchId = (long?)g.Warehouse.BranchId,
                PoBranchId = (long?)g.PurchaseOrder.BranchId
            })
            .FirstOrDefaultAsync(ct);

        if (grn == null)
        {
            return null;
        }

        return grn.WarehouseBranchId ?? grn.PoBranchId;
    }
}
