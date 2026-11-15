using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Inventory;

public interface IInventoryStockAlertStore
{
    Task<IReadOnlyList<StockAlert>> GetStockAlertsAsync(bool? resolved, long? branchId, int take, CancellationToken ct = default);
    Task RemoveUnresolvedStockAlertsAsync(long? branchId, CancellationToken ct = default);
    Task<IReadOnlyList<MyGarage.Models.Inventory>> GetActiveInventoriesForAlertsAsync(long? branchId, CancellationToken ct = default);
    Task AddStockAlertsAsync(IReadOnlyList<StockAlert> alerts, CancellationToken ct = default);
    Task<StockAlert?> GetStockAlertWithWarehouseAsync(long alertId, CancellationToken ct = default);
    Task<IReadOnlyList<MyGarage.Models.Inventory>> GetLowStockInventoriesForAlertsAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<StockAlert>> GetAdminStockAlertsAsync(bool? resolved, int take, DateTime defaultCreatedAt, CancellationToken ct = default);
    Task<IReadOnlyList<MyGarage.Models.Inventory>> GetActiveInventoriesForAdminAlertsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StockAlert>> GetUnresolvedStockAlertsTrackedAsync(CancellationToken ct = default);
    void AddStockAlert(StockAlert alert);
    Task<StockAlert?> GetStockAlertByIdAsync(long alertId, CancellationToken ct = default);
}
