using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Inventory.DTOs;

namespace MyGarage.Application.Inventory;

public interface IInventoryApplicationService
{
    Task<CatalogPageResult?> GetCatalogPageAsync(string kind, int? page, int? pageSize, CancellationToken ct = default);
    Task ExportProductsCsvAsync(Stream outputStream, CancellationToken ct = default);
    Task<PartsPageResult> GetPartsPageAsync(string? search, int? page, int? pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<StockAlertItemDto>> GetStockAlertsAsync(bool? resolved, int take, UserActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<StockAlertItemDto>> RebuildStockAlertsAsync(UserActorContext actor, CancellationToken ct = default);
    Task<bool> ResolveStockAlertAsync(long alertId, long purchaseOrderId, UserActorContext actor, CancellationToken ct = default);
    Task<StockTransactionPageResult> GetStockTransactionsAsync(
        long? branchId,
        long? warehouseId,
        long? productId,
        string? type,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        string? search,
        UserActorContext actor,
        CancellationToken ct = default);
}
