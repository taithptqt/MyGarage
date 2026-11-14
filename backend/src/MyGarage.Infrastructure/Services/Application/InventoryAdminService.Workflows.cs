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
        public async Task<object> GetTransactionsAsync(
            long? branchId,
            long? warehouseId,
            long? productId,
            string? type,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            string? search = null)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);

            var query = BuildTransactionQuery();
            query = ApplyTransactionFilters(query, branchId, warehouseId, productId, type, from, to, search);

            var total = await query.CountAsync();
            var offset = ((long)page - 1) * pageSize;
            if (offset >= total) return new { items = Array.Empty<object>(), total, page, pageSize };
            var rows = await query
                .OrderByDescending(x => x.TransactionCreatedAt)
                .ThenByDescending(x => x.Transaction.Id)
                .Skip((int)offset)
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
                .Join(
                    _context.Branches.AsNoTracking(),
                    tw => tw.w.BranchId,
                    b => b.Id,
                    (tw, b) => new TransactionQueryRow
                    {
                        Transaction = tw.t,
                        Warehouse = tw.w,
                        Branch = b,
                        TransactionCreatedAt = tw.t.CreatedAt
                    })
                .AsQueryable();
        }

        private static IQueryable<TransactionQueryRow> ApplyTransactionFilters(
            IQueryable<TransactionQueryRow> query,
            long? branchId,
            long? warehouseId,
            long? productId,
            string? type,
            DateTime? from,
            DateTime? to,
            string? search)
        {
            if (branchId.HasValue) query = query.Where(x => x.Branch.Id == branchId.Value);
            if (warehouseId.HasValue) query = query.Where(x => x.Warehouse.Id == warehouseId.Value);
            if (productId.HasValue) query = query.Where(x => x.Transaction.ProductId == productId.Value);
            if (!string.IsNullOrWhiteSpace(type))
            {
                var normalized = type.Trim().ToUpperInvariant();
                query = query.Where(x => x.Transaction.Type != null && x.Transaction.Type.ToUpper().Contains(normalized));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                query = query.Where(x =>
                    x.Transaction.Product.Sku.Contains(keyword) ||
                    x.Transaction.Product.Name.Contains(keyword) ||
                    (x.Transaction.Note != null && x.Transaction.Note.Contains(keyword)) ||
                    (x.Transaction.CreatedByNavigation != null && x.Transaction.CreatedByNavigation.FullName.Contains(keyword)));
            }
            if (from.HasValue) query = query.Where(x => x.Transaction.CreatedAt.HasValue && x.Transaction.CreatedAt.Value >= from.Value);
            if (to.HasValue) query = query.Where(x => x.Transaction.CreatedAt.HasValue && x.Transaction.CreatedAt.Value <= to.Value);
            return query;
        }

        private static object MapTransactionRow(TransactionQueryRow row)
        {
            var source = ResolveSourceType(
                row.Transaction.PurchaseOrderId,
                row.Transaction.RetailOrderId,
                row.Transaction.RetailReturnId,
                row.Transaction.MaterialRequestId,
                row.Transaction.MaterialReturnId);

            return new
            {
                id = row.Transaction.Id,
                createdAt = row.Transaction.CreatedAt,
                date = row.Transaction.CreatedAt,
                type = row.Transaction.Type,
                quantity = row.Transaction.Quantity,
                qty = row.Transaction.Quantity,
                price = row.Transaction.Price,
                note = row.Transaction.Note,
                @ref = !string.IsNullOrWhiteSpace(row.Transaction.Note) ? row.Transaction.Note : source,
                branchId = row.Branch.Id,
                branchName = row.Branch.Name,
                warehouseId = row.Warehouse.Id,
                warehouseName = row.Warehouse.Name,
                productId = row.Transaction.ProductId,
                productSku = row.Transaction.Product.Sku,
                partCode = row.Transaction.Product.Sku,
                productName = row.Transaction.Product.Name,
                partName = row.Transaction.Product.Name,
                createdBy = row.Transaction.CreatedBy,
                createdByName = row.Transaction.CreatedByNavigation != null ? row.Transaction.CreatedByNavigation.FullName : null,
                by = row.Transaction.CreatedByNavigation != null ? row.Transaction.CreatedByNavigation.FullName : null,
                source
            };
        }

        private sealed class TransactionQueryRow
        {
            public required StockTransaction Transaction { get; init; }
            public required Warehouse Warehouse { get; init; }
            public required Branch Branch { get; init; }
            public DateTime? TransactionCreatedAt { get; init; }
        }
    }
}
