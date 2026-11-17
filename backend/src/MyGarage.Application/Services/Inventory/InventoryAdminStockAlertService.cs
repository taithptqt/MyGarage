using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Inventory;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class InventoryAdminStockAlertService : IInventoryAdminStockAlertService
    {
        private readonly IInventoryStore _store;
        public InventoryAdminStockAlertService(IInventoryStore store) => _store = store;

        public async Task<IReadOnlyList<object>> GetAlertsAsync()
        {
            var now = VietnamTime.Now;
            var lowStockRows = await _store.GetLowStockInventoriesForAlertsAsync(50);
            var lowStocks = lowStockRows
                .Select(i => new AlertViewModel(
                    "LOW_STOCK",
                    "HIGH",
                    i.Product.Sku + " - " + i.Product.Name + " thấp hơn mức tối thiểu (Min).",
                    i.Warehouse.Branch.Name,
                    i.Warehouse.Name,
                    i.Id,
                    "Inventory",
                    now))
                .ToList();

            var txWithoutDocRows = await _store.GetStockTransactionsWithoutSourceAsync(30);
            var txWithoutDoc = txWithoutDocRows
                .Select(t => new AlertViewModel(
                    "TX_NO_SOURCE",
                    "HIGH",
                    "Giao dịch kho #" + t.Id + " không có chứng từ nguồn.",
                    "",
                    "",
                    t.Id,
                    "StockTransaction",
                    t.CreatedAt ?? now))
                .ToList();

            return lowStocks.Concat(txWithoutDoc)
                .OrderByDescending(a => a.CreatedAt)
                .Cast<object>()
                .ToList();
        }

        public async Task<IReadOnlyList<object>> GetStockAlertsAsync(bool? resolved = false, int take = 200)
        {
            if (take <= 0) take = 50;
            if (take > 1000) take = 1000;

            var now = VietnamTime.Now;
            var rows = await _store.GetAdminStockAlertsAsync(resolved, take, now);

            return rows
                .Select(x => (object)new
                {
                    id = x.Id,
                    type = x.AlertType,
                    severity = x.AlertType == "OutOfStock" ? "CRITICAL" : "HIGH",
                    message = (x.Product.Sku ?? "") + " - " + x.Product.Name + " (" + x.CurrentStock + ")",
                    branchId = x.Warehouse.BranchId,
                    branchName = x.Warehouse.Branch.Name,
                    warehouseId = x.WarehouseId,
                    warehouseName = x.Warehouse.Name,
                    productId = x.ProductId,
                    currentStock = x.CurrentStock,
                    minStock = x.MinStock,
                    isResolved = x.IsResolved,
                    resolvedAt = x.ResolvedAt,
                    resolvedByPurchaseOrderId = x.ResolvedByPurchaseOrderId,
                    createdAt = x.CreatedAt
                })
                .ToList();
        }

        public async Task<IReadOnlyList<object>> RebuildStockAlertsAsync(long? actorUserId = null)
        {
            var inventories = await _store.GetActiveInventoriesForAdminAlertsAsync();
            var unresolved = await _store.GetUnresolvedStockAlertsTrackedAsync();

            var activeInventoryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeAlertKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var inventory in inventories)
            {
                var qty = inventory.Quantity;
                var minStock = ResolveMinStock(inventory);
                var reorderPoint = ResolveReorderPoint(inventory);
                var alertType = ResolveAlertType(qty, minStock, reorderPoint);
                var inventoryKey = BuildInventoryKey(inventory.ProductId, inventory.WarehouseId);
                activeInventoryKeys.Add(inventoryKey);

                if (string.IsNullOrWhiteSpace(alertType))
                {
                    continue;
                }

                var key = BuildAlertKey(inventory.ProductId, inventory.WarehouseId, alertType);
                activeAlertKeys.Add(key);

                var existing = unresolved.FirstOrDefault(x =>
                    x.ProductId == inventory.ProductId &&
                    x.WarehouseId == inventory.WarehouseId &&
                    x.AlertType == alertType &&
                    x.IsResolved == false);
                if (existing != null)
                {
                    existing.CurrentStock = qty;
                    existing.MinStock = minStock;
                    continue;
                }

                _store.AddStockAlert(new StockAlert
                {
                    ProductId = inventory.ProductId,
                    WarehouseId = inventory.WarehouseId,
                    AlertType = alertType,
                    CurrentStock = qty,
                    MinStock = minStock,
                    IsResolved = false,
                    CreatedAt = VietnamTime.Now
                });
            }

            foreach (var alert in unresolved)
            {
                var inventoryKey = BuildInventoryKey(alert.ProductId, alert.WarehouseId);
                var alertKey = BuildAlertKey(alert.ProductId, alert.WarehouseId, alert.AlertType);
                if (!activeInventoryKeys.Contains(inventoryKey) || !activeAlertKeys.Contains(alertKey))
                {
                    alert.IsResolved = true;
                    alert.ResolvedAt = VietnamTime.Now;
                    alert.ResolvedByPurchaseOrderId = null;
                }
            }

            await _store.SaveChangesAsync();
            return await GetStockAlertsAsync(false, 200);
        }

        public async Task<bool> ResolveStockAlertAsync(long alertId, long purchaseOrderId, long? actorUserId = null)
        {
            if (alertId <= 0 || purchaseOrderId <= 0)
            {
                return false;
            }

            var alert = await _store.GetStockAlertByIdAsync(alertId);
            if (alert == null || alert.IsResolved)
            {
                return false;
            }

            var purchaseOrderExists = await _store.PurchaseOrderExistsAsync(purchaseOrderId);
            if (!purchaseOrderExists)
            {
                return false;
            }

            alert.IsResolved = true;
            alert.ResolvedAt = VietnamTime.Now;
            alert.ResolvedByPurchaseOrderId = purchaseOrderId;
            await _store.SaveChangesAsync();
            return true;
        }

        private static decimal ResolveMinStock(Inventory inventory)
        {
            if (inventory.MinStockLevel.HasValue)
            {
                return Math.Max(0m, inventory.MinStockLevel.Value);
            }

            if (inventory.Product?.MinStockLevel.HasValue == true)
            {
                return Math.Max(0m, inventory.Product.MinStockLevel.Value);
            }

            return 0m;
        }

        private static decimal ResolveReorderPoint(Inventory inventory)
        {
            if (inventory.Product?.ReorderPoint.HasValue == true)
            {
                return Math.Max(0m, inventory.Product.ReorderPoint.Value);
            }

            return ResolveMinStock(inventory);
        }

        private static string ResolveAlertType(decimal quantity, decimal minStock, decimal reorderPoint)
        {
            if (quantity <= 0m)
            {
                return "OutOfStock";
            }

            if (minStock > 0m && quantity <= minStock)
            {
                return "BelowMinStock";
            }

            if (reorderPoint > 0m && quantity <= reorderPoint)
            {
                return "BelowReorder";
            }

            return string.Empty;
        }

        private static string BuildAlertKey(long productId, long warehouseId, string alertType)
            => $"{productId}:{warehouseId}:{alertType}";

        private static string BuildInventoryKey(long productId, long warehouseId)
            => $"{productId}:{warehouseId}";

        private sealed record AlertViewModel(
            string Type,
            string Severity,
            string Message,
            string BranchName,
            string WarehouseName,
            long ReferenceId,
            string ReferenceType,
            DateTime CreatedAt);
    }
}
