using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class InventoryAdminService
    {
        public async Task<IReadOnlyList<object>> GetAlertsAsync()
        {
            var now = VietnamTime.Now;
            var lowStocks = await _context.Inventories
                .AsNoTracking()
                .Include(i => i.Product)
                .Include(i => i.Warehouse)
                    .ThenInclude(w => w.Branch)
                .Where(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel.Value)
                .OrderBy(i => i.Quantity)
                .Take(50)
                .Select(i => new AlertViewModel(
                    "LOW_STOCK",
                    "HIGH",
                    i.Product.Sku + " - " + i.Product.Name + " th?p hon Min.",
                    i.Warehouse.Branch.Name,
                    i.Warehouse.Name,
                    i.Id,
                    "Inventory",
                    now))
                .ToListAsync();

            var txWithoutDoc = await _context.StockTransactions
                .AsNoTracking()
                .Where(t =>
                    !t.RetailOrderId.HasValue &&
                    !t.RetailReturnId.HasValue &&
                    !t.MaterialRequestId.HasValue &&
                    !t.MaterialReturnId.HasValue &&
                    !t.PurchaseOrderId.HasValue)
                .OrderByDescending(t => t.CreatedAt)
                .Take(30)
                .Select(t => new AlertViewModel(
                    "TX_NO_SOURCE",
                    "HIGH",
                    "Giao d?ch kho #" + t.Id + " không có ch?ng t? ngu?n.",
                    "",
                    "",
                    t.Id,
                    "StockTransaction",
                    t.CreatedAt ?? now))
                .ToListAsync();

            return lowStocks.Concat(txWithoutDoc)
                .OrderByDescending(a => a.CreatedAt)
                .Cast<object>()
                .ToList();
        }
    }
}

