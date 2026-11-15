using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfRetailReturnDataService(IGarageContext context) : IRetailReturnDataService
{
    public async Task<(IReadOnlyList<RetailReturn> Items, int Total)> GetPagedAsync(
        long? branchId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.RetailReturns.AsNoTracking().AsQueryable();
        if (branchId.HasValue)
        {
            query = query.Where(x => x.RetailOrder.BranchId == branchId.Value);
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var paged = offset >= total
            ? new List<RetailReturn>()
            : await query.OrderByDescending(x => x.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return (paged, total);
    }

    public Task<RetailReturn?> GetByIdAsync(long id, long? branchId, CancellationToken ct = default) =>
        context.RetailReturns.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == id && (!branchId.HasValue || x.RetailOrder.BranchId == branchId.Value),
            ct);

    public Task<bool> RetailOrderExistsAsync(long retailOrderId, long? branchId, CancellationToken ct = default) =>
        context.RetailOrders.AsNoTracking().AnyAsync(
            x => x.Id == retailOrderId && (!branchId.HasValue || x.BranchId == branchId.Value),
            ct);

    public Task<RetailReturn?> GetForUpdateAsync(long id, long? branchId, CancellationToken ct = default) =>
        context.RetailReturns.Include(x => x.RetailOrder).FirstOrDefaultAsync(
            x => x.Id == id && (!branchId.HasValue || x.RetailOrder.BranchId == branchId.Value),
            ct);

    public async Task<RetailReturnFinalizeResult> FinalizeReturnAsync(
        long id,
        long actorUserId,
        long? scopedBranch,
        CancellationToken ct = default)
    {
        if (context.Database.CurrentTransaction != null)
        {
            throw new InvalidOperationException("Không thể hoàn tất trả hàng trong giao dịch đang mở.");
        }

        // Lock the order rather than just the return: several returns can reference
        // the same sold lines. Reload all state within each retry and transaction.
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            var orderId = await context.RetailReturns.AsNoTracking()
                .Where(x => x.Id == id && (!scopedBranch.HasValue || x.RetailOrder.BranchId == scopedBranch.Value))
                .Select(x => (long?)x.RetailOrderId)
                .FirstOrDefaultAsync(ct);
            if (!orderId.HasValue)
            {
                return new RetailReturnFinalizeResult(RetailReturnFinalizeStatus.NotFound, "Không tìm thấy phiếu trả hàng.");
            }

            await using var tx = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
                : await TryBeginTransactionAsync(ct);
            var committing = false;
            try
            {
                if (context.Database.IsSqlServer())
                {
                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT [Id] FROM [RetailOrders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {orderId.Value}",
                        ct);
                }

                var retailReturn = await context.RetailReturns
                    .Include(x => x.RetailReturnItems).ThenInclude(x => x.RetailOrderItem)
                    .Include(x => x.RetailOrder)
                    .FirstOrDefaultAsync(
                        x => x.Id == id && x.RetailOrderId == orderId.Value &&
                             (!scopedBranch.HasValue || x.RetailOrder.BranchId == scopedBranch.Value),
                        ct);
                if (retailReturn == null)
                {
                    return new RetailReturnFinalizeResult(RetailReturnFinalizeStatus.NotFound, "Không tìm thấy phiếu trả hàng.");
                }

                if (string.Equals(retailReturn.Status, nameof(RetailReturnStatusCode.COMPLETED), StringComparison.OrdinalIgnoreCase))
                {
                    return new RetailReturnFinalizeResult(
                        RetailReturnFinalizeStatus.AlreadyCompleted,
                        "Phiếu trả hàng đã hoàn tất.",
                        RetailReturnId: id);
                }

                if (!string.Equals(retailReturn.Status, nameof(RetailReturnStatusCode.PENDING), StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(retailReturn.Status, nameof(RetailReturnStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase))
                {
                    return new RetailReturnFinalizeResult(
                        RetailReturnFinalizeStatus.BadRequest,
                        "Trạng thái phiếu trả hàng không hợp lệ để hoàn tất.");
                }

                if (retailReturn.RetailReturnItems.Count == 0)
                {
                    return new RetailReturnFinalizeResult(
                        RetailReturnFinalizeStatus.BadRequest,
                        "Phiếu trả hàng chưa có dòng chi tiết.");
                }

                var warehouseIds = retailReturn.RetailReturnItems.Select(x => x.WarehouseId).Distinct().ToArray();
                var validWarehouseIds = await context.Warehouses.AsNoTracking()
                    .Where(x => warehouseIds.Contains(x.Id) && x.BranchId == retailReturn.RetailOrder.BranchId)
                    .Select(x => x.Id)
                    .ToListAsync(ct);
                if (validWarehouseIds.Count != warehouseIds.Length)
                {
                    return new RetailReturnFinalizeResult(
                        RetailReturnFinalizeStatus.BadRequest,
                        "Kho trả hàng không thuộc chi nhánh của đơn bán lẻ.");
                }

                foreach (var group in retailReturn.RetailReturnItems.GroupBy(x => x.RetailOrderItemId))
                {
                    var item = group.First().RetailOrderItem;
                    if (item == null || item.RetailOrderId != retailReturn.RetailOrderId)
                    {
                        return new RetailReturnFinalizeResult(
                            RetailReturnFinalizeStatus.BadRequest,
                            "Dòng trả hàng không thuộc đơn bán lẻ.");
                    }

                    if (group.Any(line => line.ReturnedQuantity <= 0m || line.RefundAmount < 0m))
                    {
                        return new RetailReturnFinalizeResult(
                            RetailReturnFinalizeStatus.BadRequest,
                            "Số lượng trả hoặc giá trị hoàn không hợp lệ.");
                    }

                    var alreadyReturnedQty = await context.RetailReturnItems.AsNoTracking()
                        .Where(x => x.RetailOrderItemId == item.Id && x.RetailReturnId != retailReturn.Id &&
                                    x.Status != nameof(RetailReturnStatusCode.REJECTED))
                        .SumAsync(x => (decimal?)x.ReturnedQuantity, ct) ?? 0m;
                    if (alreadyReturnedQty + group.Sum(line => line.ReturnedQuantity) > item.Quantity + 0.0001m)
                    {
                        return new RetailReturnFinalizeResult(
                            RetailReturnFinalizeStatus.BadRequest,
                            $"Số lượng trả vượt quá số đã bán cho item #{item.Id}.");
                    }
                }

                var totalRefund = retailReturn.RetailReturnItems.Sum(line => line.RefundAmount);
                if (totalRefund > retailReturn.RetailOrder.FinalAmount)
                {
                    return new RetailReturnFinalizeResult(
                        RetailReturnFinalizeStatus.BadRequest,
                        "Giá trị hoàn vượt quá giá trị còn lại của đơn bán lẻ.");
                }

                var now = VietnamTime.Now;
                foreach (var line in retailReturn.RetailReturnItems)
                {
                    line.Status = nameof(RetailReturnStatusCode.COMPLETED);
                    line.UpdatedAt = now;
                    line.UpdatedBy = actorUserId;
                    var hasStockTransaction = await context.StockTransactions.AsNoTracking()
                        .AnyAsync(
                            x => x.RetailReturnId == retailReturn.Id &&
                                 x.ProductId == line.RetailOrderItem.ProductId &&
                                 x.WarehouseId == line.WarehouseId,
                            ct);
                    if (!hasStockTransaction)
                    {
                        context.StockTransactions.Add(new StockTransaction
                        {
                            ProductId = line.RetailOrderItem.ProductId,
                            WarehouseId = line.WarehouseId,
                            Type = "IN",
                            Quantity = line.ReturnedQuantity,
                            Price = line.RefundAmount,
                            Note = $"Retail return #{retailReturn.ReturnCode}",
                            RetailOrderId = retailReturn.RetailOrderId,
                            RetailReturnId = retailReturn.Id,
                            CreatedAt = now,
                            CreatedBy = actorUserId
                        });
                    }
                }

                retailReturn.TotalRefundAmount = totalRefund;
                retailReturn.Status = nameof(RetailReturnStatusCode.COMPLETED);
                retailReturn.ApprovedBy = actorUserId;
                retailReturn.ApprovedAt = now;
                retailReturn.UpdatedAt = now;
                retailReturn.UpdatedBy = actorUserId;
                var order = retailReturn.RetailOrder;
                order.FinalAmount = Math.Max(0m, order.FinalAmount - totalRefund);
                order.UpdatedAt = now;
                order.UpdatedBy = actorUserId;
                await SaveChangesInTransactionAsync(ct);
                if (tx != null)
                {
                    committing = true;
                    await tx.CommitAsync(ct);
                    committing = false;
                }

                return new RetailReturnFinalizeResult(
                    RetailReturnFinalizeStatus.Ok,
                    "Đã hoàn tất trả hàng.",
                    RetailReturnId: retailReturn.Id,
                    Status: retailReturn.Status,
                    TotalRefundAmount: retailReturn.TotalRefundAmount);
            }
            catch (Exception ex)
            {
                if (tx != null)
                {
                    try
                    {
                        await tx.RollbackAsync(ct);
                    }
                    catch
                    {
                        /* Preserve original error. */
                    }
                }

                context.ChangeTracker.Clear();
                if (committing)
                {
                    throw new InvalidOperationException(
                        "Không xác định được kết quả trả hàng; kiểm tra phiếu trước khi thử lại.",
                        ex);
                }

                throw;
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct = default)
    {
        if (context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await context.Database.BeginTransactionAsync(ct);
    }

    private async Task SaveChangesInTransactionAsync(CancellationToken ct = default)
    {
        await using var tx = await TryBeginTransactionAsync(ct);
        await context.SaveChangesAsync(ct);
        if (tx != null)
        {
            await tx.CommitAsync(ct);
        }
    }
}
