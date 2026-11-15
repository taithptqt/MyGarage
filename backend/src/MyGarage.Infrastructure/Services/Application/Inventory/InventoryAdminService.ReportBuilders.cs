using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class InventoryAdminService
    {
        private async Task<List<Inventory>> LoadInventoryRowsForReportAsync()
        {
            return await _context.Inventories
                .AsNoTracking()
                .Include(i => i.Product)
                .Include(i => i.Warehouse)
                    .ThenInclude(w => w.Branch)
                .ToListAsync();
        }

        private async Task<List<StockTransaction>> LoadTransactionsForReportAsync()
        {
            return await _context.StockTransactions
                .AsNoTracking()
                .Include(t => t.Product)
                .ToListAsync();
        }

        private static List<object> BuildStockByBranch(IEnumerable<Inventory> inventoryRows)
        {
            return inventoryRows
                .GroupBy(i => new { i.Warehouse.BranchId, i.Warehouse.Branch.Name })
                .Select(g => new StockByBranchViewModel(
                    g.Key.BranchId,
                    g.Key.Name,
                    g.Sum(x => x.Quantity),
                    g.Sum(x => x.TotalInventoryValue ?? (x.Quantity * (x.CurrentMacPrice ?? x.Product.CostPrice ?? 0))))
                )
                .OrderBy(x => x.BranchName)
                .Cast<object>()
                .ToList();
        }

        private static List<object> BuildTopOutboundProducts(IEnumerable<StockTransaction> transactions)
        {
            return transactions
                .Where(t => IsOutboundType(t.Type))
                .GroupBy(t => new { t.ProductId, t.Product.Sku, t.Product.Name })
                .Select(g => new TopOutboundProductViewModel(
                    g.Key.ProductId,
                    g.Key.Sku,
                    g.Key.Name,
                    g.Sum(x => Math.Abs(x.Quantity)))
                )
                .OrderByDescending(x => x.TotalOutboundQty)
                .Take(20)
                .Cast<object>()
                .ToList();
        }

        private static bool IsOutboundType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return false;
            var normalized = type.Trim().ToUpperInvariant();
            return normalized.Contains("OUT") || normalized.Contains("ISSUE") || normalized.Contains("XUAT");
        }

        private static string ResolveSourceType(
            long? purchaseOrderId,
            long? retailOrderId,
            long? retailReturnId,
            long? materialRequestId,
            long? materialReturnId)
        {
            if (purchaseOrderId.HasValue) return "PurchaseOrder";
            if (retailOrderId.HasValue) return "RetailOrder";
            if (retailReturnId.HasValue) return "RetailReturn";
            if (materialRequestId.HasValue) return "MaterialRequest";
            if (materialReturnId.HasValue) return "MaterialReturn";
            return "Unknown";
        }

        private sealed record StockByBranchViewModel(
            long BranchId,
            string BranchName,
            decimal TotalQuantity,
            decimal TotalValue
        );

        private sealed record TopOutboundProductViewModel(
            long ProductId,
            string Sku,
            string ProductName,
            decimal TotalOutboundQty
        );
    }
}