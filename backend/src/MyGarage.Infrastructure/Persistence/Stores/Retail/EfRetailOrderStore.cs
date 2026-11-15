using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Retail;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Infrastructure.Retail;

public sealed class EfRetailOrderStore : IRetailOrderStore
{
    private readonly IGarageContext _context;

    public EfRetailOrderStore(IGarageContext context)
    {
        _context = context;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _context.Database.BeginTransactionAsync();
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

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                var result = await action(ct);
                if (tx != null) await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<bool> ActiveBranchExistsAsync(long branchId, CancellationToken ct = default)
    {
        return await _context.Branches
            .AsNoTracking()
            .AnyAsync(branch => branch.Id == branchId && branch.IsDeleted != true, ct);
    }

    public async Task<IReadOnlyList<Product>> GetActiveProductsByIdsAsync(IReadOnlyCollection<long> productIds, CancellationToken ct = default)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(product =>
                productIds.Contains(product.Id) &&
                product.IsDeleted != true &&
                product.IsActive != false)
            .ToListAsync(ct);
    }

    public Task<IReadOnlyDictionary<long, int>> GetAvailableStockQuantitiesAsync(
        IReadOnlyCollection<long> productIds,
        long branchId,
        CancellationToken ct = default)
    {
        return RetailStockAvailabilityResolver.GetAvailableQuantitiesAsync(_context, productIds, branchId);
    }

    public async Task<Voucher?> GetVoucherByCodeAsync(string normalizedCode, CancellationToken ct = default)
    {
        return await _context.Vouchers
            .FirstOrDefaultAsync(v => v.Code == normalizedCode && v.IsDeleted != true, ct);
    }

    public async Task<int> CountCustomerRedeemedUsesAsync(long voucherId, long customerId, CancellationToken ct = default)
    {
        return await _context.VoucherRedemptions
            .AsNoTracking()
            .CountAsync(r => r.VoucherId == voucherId && r.CustomerId == customerId && r.RedemptionStatus == "REDEEMED", ct);
    }

    public async Task SaveCheckoutOrderAsync(
        RetailOrder order,
        IReadOnlyList<RetailOrderItem> orderItems,
        VoucherRedemption? redemptionRecord,
        CancellationToken ct = default)
    {
        _context.RetailOrders.Add(order);
        await _context.SaveChangesAsync(ct);

        foreach (var item in orderItems)
        {
            item.RetailOrderId = order.Id;
        }
        _context.RetailOrderItems.AddRange(orderItems);
        await _context.SaveChangesAsync(ct);

        if (redemptionRecord != null)
        {
            redemptionRecord.RetailOrderId = order.Id;
            _context.VoucherRedemptions.Add(redemptionRecord);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<RetailOrder?> GetOrderWithItemsByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.RetailOrders
            .AsNoTracking()
            .Include(o => o.RetailOrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<RetailOrder?> GetOrderForMutationAsync(long id, CancellationToken ct = default)
    {
        return await _context.RetailOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<IReadOnlyList<RetailOrder>> GetOrdersPageAsync(
        long? branchId,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.RetailOrders.AsNoTracking();

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(o => o.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(o => o.Status == normalizedStatus);
        }

        return await query
            .Include(o => o.RetailOrderItems)
                .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<RetailCustomerOrderPageData> GetCustomerOrdersPageAsync(
        long customerId,
        long? anchorOrderId,
        int requestedPage,
        int pageSize,
        CancellationToken ct = default)
    {
        var baseQuery = _context.RetailOrders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId);

        var totalCount = await baseQuery.CountAsync(ct);
        var draftCount = await baseQuery.CountAsync(o => o.Status == nameof(RetailOrderStatus.DRAFT), ct);
        var confirmedCount = await baseQuery.CountAsync(o => o.Status == nameof(RetailOrderStatus.CONFIRMED), ct);
        var totalValue = await baseQuery.SumAsync(o => (decimal?)o.FinalAmount, ct) ?? 0m;

        var page = requestedPage;
        if (anchorOrderId.HasValue && anchorOrderId.Value > 0)
        {
            var orderedIds = await baseQuery
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .Select(o => o.Id)
                .ToListAsync(ct);

            var index = orderedIds.IndexOf(anchorOrderId.Value);
            if (index >= 0)
            {
                page = (index / pageSize) + 1;
            }
        }

        var orders = await baseQuery
            .Include(o => o.RetailOrderItems)
                .ThenInclude(i => i.Product)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new RetailCustomerOrderPageData(orders, totalCount, page, draftCount, confirmedCount, totalValue);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }

    public async Task RemoveOrderAsync(RetailOrder order, CancellationToken ct = default)
    {
        _context.RetailOrders.Remove(order);
        await _context.SaveChangesAsync(ct);
    }
}
