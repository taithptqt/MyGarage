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
        public async Task<object> GetTransactionsAsync(long? branchId, long? warehouseId, long? productId, string? type, DateTime? from, DateTime? to, int page, int pageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);

            var query = BuildTransactionQuery();
            query = ApplyTransactionFilters(query, branchId, warehouseId, productId, type, from, to);

            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(x => x.Transaction.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = rows.Select(MapTransactionRow);
            return new { items, total, page, pageSize };
        }

        private IQueryable<TransactionQueryRow> BuildTransactionQuery()
        {
            return _context.StockTransactions
                .AsNoTracking()
                .Include(t => t.Product)
                .Include(t => t.CreatedByNavigation)
                .Join(_context.Warehouses.AsNoTracking(), t => t.WarehouseId, w => w.Id, (t, w) => new { t, w })
                .Join(_context.Branches.AsNoTracking(), tw => tw.w.BranchId, b => b.Id, (tw, b) => new TransactionQueryRow(tw.t, tw.w, b))
                .AsQueryable();
        }

        private static IQueryable<TransactionQueryRow> ApplyTransactionFilters(
            IQueryable<TransactionQueryRow> query,
            long? branchId,
            long? warehouseId,
            long? productId,
            string? type,
            DateTime? from,
            DateTime? to)
        {
            if (branchId.HasValue) query = query.Where(x => x.Branch.Id == branchId.Value);
            if (warehouseId.HasValue) query = query.Where(x => x.Warehouse.Id == warehouseId.Value);
            if (productId.HasValue) query = query.Where(x => x.Transaction.ProductId == productId.Value);
            if (!string.IsNullOrWhiteSpace(type))
            {
                var normalized = type.Trim().ToUpperInvariant();
                query = query.Where(x => x.Transaction.Type != null && x.Transaction.Type.ToUpper().Contains(normalized));
            }
            if (from.HasValue) query = query.Where(x => x.Transaction.CreatedAt.HasValue && x.Transaction.CreatedAt.Value >= from.Value);
            if (to.HasValue) query = query.Where(x => x.Transaction.CreatedAt.HasValue && x.Transaction.CreatedAt.Value <= to.Value);
            return query;
        }

        private static object MapTransactionRow(TransactionQueryRow row)
        {
            return new
            {
                id = row.Transaction.Id,
                createdAt = row.Transaction.CreatedAt,
                type = row.Transaction.Type,
                quantity = row.Transaction.Quantity,
                price = row.Transaction.Price,
                note = row.Transaction.Note,
                branchId = row.Branch.Id,
                branchName = row.Branch.Name,
                warehouseId = row.Warehouse.Id,
                warehouseName = row.Warehouse.Name,
                productId = row.Transaction.ProductId,
                productSku = row.Transaction.Product.Sku,
                productName = row.Transaction.Product.Name,
                createdBy = row.Transaction.CreatedBy,
                createdByName = row.Transaction.CreatedByNavigation != null ? row.Transaction.CreatedByNavigation.FullName : null,
                source = ResolveSourceType(
                    row.Transaction.PurchaseOrderId,
                    row.Transaction.RetailOrderId,
                    row.Transaction.RetailReturnId,
                    row.Transaction.MaterialRequestId,
                    row.Transaction.MaterialReturnId)
            };
        }

        private sealed record TransactionQueryRow(StockTransaction Transaction, Warehouse Warehouse, Branch Branch);
    }
}
