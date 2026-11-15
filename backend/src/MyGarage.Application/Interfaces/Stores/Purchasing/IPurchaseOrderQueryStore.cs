using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Purchasing;

public interface IPurchaseOrderQueryStore
{
    Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> GetPurchaseOrdersPageAsync(
        long? branchId,
        string? statusGroup,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PurchaseOrder?> GetPurchaseOrderDetailByIdAsync(long id, CancellationToken ct = default);

    Task<bool> SupplierExistsAsync(long supplierId, CancellationToken ct = default);

    Task<long> GetFirstActiveWarehouseIdAsync(long branchId, CancellationToken ct = default);

    Task<bool> WarehouseBelongsToBranchAsync(long warehouseId, long branchId, CancellationToken ct = default);

    Task<bool> ProductExistsAsync(long productId, CancellationToken ct = default);

    Task<PurchaseOrder?> GetPurchaseOrderBasicAsync(long id, CancellationToken ct = default);

    Task<PurchaseOrder?> GetPurchaseOrderForFulfillmentAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseOrder>> GetPurchaseOrdersForDashboardAsync(
        long? branchId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct = default);
}
