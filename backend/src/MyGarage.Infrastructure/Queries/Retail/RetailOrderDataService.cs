using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Domain.Rules;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfRetailOrderDataService(IGarageContext db) : IRetailOrderDataService
{
    private static string GenerateRetailOrderCode() =>
        $"RO{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(10, 99)}";

    private static decimal RoundCurrency(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public async Task<(IReadOnlyList<RetailOrder> Items, int Total)> GetPagedByBranchAsync(
        long? scopedBranchId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.RetailOrders.AsNoTracking().AsQueryable();
        if (scopedBranchId.HasValue)
        {
            query = query.Where(order => order.BranchId == scopedBranchId.Value);
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var paged = offset >= total
            ? new List<RetailOrder>()
            : await query.OrderByDescending(order => order.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return (paged, total);
    }

    public async Task<(IReadOnlyList<RetailOrder> Items, int Total)> GetPagedByCustomerAsync(
        long customerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.RetailOrders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(order => order.Id);
        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var paged = offset >= total
            ? new List<RetailOrder>()
            : await query.Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return (paged, total);
    }

    public async Task<RetailOrderMinePageResult> GetMinePageAsync(
        long customerId,
        int page,
        int pageSize,
        long? anchorId,
        CancellationToken ct = default)
    {
        var query = db.RetailOrders.AsNoTracking().Where(order => order.CustomerId == customerId);

        var summary = await query.GroupBy(_ => 1).Select(group => new
        {
            Total = group.Count(),
            DraftCount = group.Count(order => order.Status == null || order.Status == "" || order.Status.ToUpper() == "DRAFT"),
            ConfirmedCount = group.Count(order => order.Status != null && order.Status.ToUpper() == "CONFIRMED"),
            TotalValue = group.Sum(order => order.FinalAmount)
        }).FirstOrDefaultAsync(ct);
        var total = summary?.Total ?? 0;

        if (anchorId > 0)
        {
            var anchor = await query.Where(order => order.Id == anchorId).Select(order => new { order.Id, order.CreatedAt }).FirstOrDefaultAsync(ct);
            if (anchor != null)
            {
                var anchorDate = anchor.CreatedAt ?? DateTime.MinValue;
                var preceding = await query.CountAsync(order => (order.CreatedAt ?? DateTime.MinValue) > anchorDate ||
                    ((order.CreatedAt ?? DateTime.MinValue) == anchorDate && order.Id > anchor.Id), ct);
                page = preceding / pageSize + 1;
            }
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);
        var offset = ((long)page - 1) * pageSize;
        var items = await query.OrderByDescending(order => order.CreatedAt ?? DateTime.MinValue)
            .ThenByDescending(order => order.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return new RetailOrderMinePageResult(
            items,
            page,
            pageSize,
            total,
            summary?.DraftCount ?? 0,
            summary?.ConfirmedCount ?? 0,
            summary?.TotalValue ?? 0m);
    }

    public Task<RetailOrder?> GetWithItemsByIdAsync(long id, CancellationToken ct = default) =>
        db.RetailOrders
            .AsNoTracking()
            .Include(order => order.RetailOrderItems)
                .ThenInclude(orderItem => orderItem.Product)
            .FirstOrDefaultAsync(order => order.Id == id, ct);

    public async Task<RetailCheckoutResult> CheckoutAsync(
        long customerId,
        long branchId,
        IReadOnlyList<(long ProductId, int Quantity)> normalizedItems,
        string? voucherCode,
        string? customerNotes,
        CancellationToken ct = default)
    {
        var branchExists = await db.Branches
            .AsNoTracking()
            .AnyAsync(branch => branch.Id == branchId && branch.IsDeleted != true, ct);

        if (!branchExists)
        {
            return new RetailCheckoutResult(
                false,
                null,
                new { message = "Selected branch is invalid.", branchId },
                null);
        }

        var productIds = normalizedItems.Select(item => item.ProductId).ToList();
        var products = await db.Products
            .AsNoTracking()
            .Where(product =>
                productIds.Contains(product.Id) &&
                product.IsDeleted != true &&
                product.IsActive != false)
            .ToListAsync(ct);

        if (products.Count != productIds.Count)
        {
            var availableProductIds = products.Select(product => product.Id).ToHashSet();
            var missingProductIds = productIds
                .Where(productId => !availableProductIds.Contains(productId))
                .OrderBy(productId => productId)
                .ToList();

            return new RetailCheckoutResult(
                false,
                null,
                new
                {
                    message = "One or more selected retail products are invalid or inactive.",
                    productIds = missingProductIds
                },
                null);
        }

        var productMap = products.ToDictionary(product => product.Id);
        var availabilityMap = await RetailStockAvailabilityResolver.GetAvailableQuantitiesAsync(
            db,
            productIds,
            branchId,
            ct);

        var stockIssues = normalizedItems
            .Select(item =>
            {
                var product = productMap[item.ProductId];
                availabilityMap.TryGetValue(item.ProductId, out var availableQuantity);

                return new
                {
                    productId = item.ProductId,
                    productName = product.Name,
                    requestedQuantity = item.Quantity,
                    availableQuantity
                };
            })
            .Where(item => item.requestedQuantity > item.availableQuantity)
            .OrderBy(item => item.productId)
            .ToList();

        if (stockIssues.Count > 0)
        {
            return new RetailCheckoutResult(
                false,
                null,
                new
                {
                    message = "Một hoặc nhiều phụ tùng không còn đủ tồn kho tại chi nhánh đã chọn.",
                    items = stockIssues
                },
                null);
        }

        var retailOrderItems = normalizedItems
            .Select(item =>
            {
                var product = productMap[item.ProductId];
                var unitPrice = RoundCurrency(product.RetailPrice ?? 0m);
                var subTotal = RoundCurrency(unitPrice * item.Quantity);
                var taxRate = product.TaxRate ?? 0m;
                var taxAmount = RoundCurrency(subTotal * taxRate / 100m);
                var finalAmount = RoundCurrency(subTotal + taxAmount);

                return new RetailOrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    CostPriceAtTime = product.CostPrice,
                    UnitPriceAtTime = unitPrice,
                    SubTotal = subTotal,
                    TaxRate = taxRate,
                    TaxAmount = taxAmount,
                    FinalAmount = finalAmount
                };
            })
            .ToList();

        var subTotalAmount = RoundCurrency(retailOrderItems.Sum(item => item.SubTotal));
        var taxAmountTotal = RoundCurrency(retailOrderItems.Sum(item => item.TaxAmount ?? 0m));
        var now = VietnamTime.Now;

        Voucher? voucherToRedeem = null;
        decimal voucherDiscount = 0m;

        if (!string.IsNullOrWhiteSpace(voucherCode))
        {
            var normCode = voucherCode.Trim().ToUpperInvariant();
            var candidateVoucher = await db.Vouchers
                .FirstOrDefaultAsync(v => v.Code == normCode && v.IsDeleted != true, ct);

            if (candidateVoucher == null)
            {
                return new RetailCheckoutResult(false, $"Mã voucher '{voucherCode}' không tồn tại.", null, null);
            }

            if (!string.Equals(candidateVoucher.Status, VoucherLifecycleRules.StatusActive, StringComparison.OrdinalIgnoreCase) || candidateVoucher.IsActive != true)
            {
                return new RetailCheckoutResult(false, $"Voucher '{candidateVoucher.Code}' không ở trạng thái khả dụng ({candidateVoucher.Status}).", null, null);
            }

            if (!VoucherLifecycleRules.IsDateRangeValid(now, candidateVoucher.StartDate, candidateVoucher.EndDate))
            {
                return new RetailCheckoutResult(false, $"Voucher '{candidateVoucher.Code}' đã hết hạn hoặc chưa đến ngày bắt đầu.", null, null);
            }

            if (!VoucherLifecycleRules.IsEligibleSpend(subTotalAmount, candidateVoucher.MinOrderValue))
            {
                return new RetailCheckoutResult(false, $"Đơn hàng chưa đạt giá trị tối thiểu để áp dụng voucher ({candidateVoucher.MinOrderValue:N0}đ).", null, null);
            }

            if (!VoucherLifecycleRules.CanRedeemTotalQuota(candidateVoucher.UsageLimit, candidateVoucher.UsedCount))
            {
                return new RetailCheckoutResult(false, $"Voucher '{candidateVoucher.Code}' đã hết lượt sử dụng.", null, null);
            }

            if (candidateVoucher.MaxUsesPerCustomer > 0)
            {
                var customerUses = await db.VoucherRedemptions
                    .AsNoTracking()
                    .CountAsync(r => r.VoucherId == candidateVoucher.Id && r.CustomerId == customerId && r.RedemptionStatus == "REDEEMED", ct);
                if (!VoucherLifecycleRules.CanRedeemCustomerQuota(candidateVoucher.MaxUsesPerCustomer, customerUses))
                {
                    return new RetailCheckoutResult(false, $"Bạn đã dùng hết hạn mức của voucher này ({candidateVoucher.MaxUsesPerCustomer} lần).", null, null);
                }
            }

            voucherDiscount = VoucherLifecycleRules.CalculateDiscount(
                candidateVoucher.Type,
                candidateVoucher.Value,
                candidateVoucher.MaxDiscountAmount,
                subTotalAmount);

            if (!VoucherLifecycleRules.CanRedeemBudget(candidateVoucher.BudgetLimit, candidateVoucher.BudgetUsed, voucherDiscount))
            {
                return new RetailCheckoutResult(false, $"Ngân sách cho voucher '{candidateVoucher.Code}' đã hết.", null, null);
            }

            voucherToRedeem = candidateVoucher;
        }

        var totals = RetailOrderLifecycleRules.CalculateOrderTotals(subTotalAmount, taxAmountTotal, voucherDiscount, 0m);

        var retailOrder = new RetailOrder
        {
            BranchId = branchId,
            OrderCode = GenerateRetailOrderCode(),
            CustomerId = customerId,
            SubTotal = totals.SubTotal,
            TierDiscountAmount = 0m,
            VoucherId = voucherToRedeem?.Id,
            VoucherDiscountAmount = voucherDiscount,
            TotalDiscountAmount = totals.TotalDiscount,
            TaxAmount = totals.TaxAmount,
            FinalAmount = totals.FinalAmount,
            Status = nameof(RetailOrderStatus.DRAFT),
            CustomerNotes = string.IsNullOrWhiteSpace(customerNotes)
                ? null
                : customerNotes.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await TryBeginTransactionAsync(ct);
            try
            {
                db.RetailOrders.Add(retailOrder);
                await SaveChangesInTransactionAsync(ct);

                foreach (var item in retailOrderItems)
                {
                    item.RetailOrderId = retailOrder.Id;
                }

                db.RetailOrderItems.AddRange(retailOrderItems);
                await SaveChangesInTransactionAsync(ct);

                if (voucherToRedeem != null)
                {
                    var redemption = new VoucherRedemption
                    {
                        VoucherId = voucherToRedeem.Id,
                        VoucherCode = voucherToRedeem.Code,
                        CustomerId = customerId,
                        BranchId = branchId,
                        RetailOrderId = retailOrder.Id,
                        ValidationStatus = "VALIDATED",
                        RedemptionStatus = "REDEEMED",
                        EligibleAmount = totals.SubTotal,
                        DiscountAmount = voucherDiscount,
                        BudgetImpact = voucherDiscount,
                        AppliedBy = customerId,
                        IdempotencyKey = $"ro-checkout-{retailOrder.OrderCode}",
                        AppliedAt = now,
                        RedeemedAt = now,
                        CreatedAt = now
                    };
                    db.VoucherRedemptions.Add(redemption);

                    voucherToRedeem.UsedCount = (voucherToRedeem.UsedCount ?? 0) + 1;
                    voucherToRedeem.RedeemedCount += 1;
                    voucherToRedeem.BudgetUsed += voucherDiscount;
                    voucherToRedeem.UpdatedAt = now;
                    voucherToRedeem.UpdatedBy = customerId;

                    var exhaustedByUsage = voucherToRedeem.UsageLimit.HasValue && voucherToRedeem.UsageLimit.Value > 0 && voucherToRedeem.UsedCount >= voucherToRedeem.UsageLimit.Value;
                    var exhaustedByBudget = voucherToRedeem.BudgetLimit.HasValue && voucherToRedeem.BudgetUsed >= voucherToRedeem.BudgetLimit.Value;
                    if (exhaustedByUsage || exhaustedByBudget)
                    {
                        voucherToRedeem.Status = VoucherLifecycleRules.StatusExhausted;
                        voucherToRedeem.IsActive = false;
                    }

                    await SaveChangesInTransactionAsync(ct);
                }

                if (transaction != null) await transaction.CommitAsync(ct);

                return retailOrder;
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync(ct);
                throw;
            }
        });

        return new RetailCheckoutResult(true, null, null, result);
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await db.Database.BeginTransactionAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return null;
            }
            throw;
        }
    }

    private async Task SaveChangesInTransactionAsync(CancellationToken ct = default)
    {
        await using var tx = await TryBeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        if (tx != null)
        {
            await tx.CommitAsync(ct);
        }
    }
}
