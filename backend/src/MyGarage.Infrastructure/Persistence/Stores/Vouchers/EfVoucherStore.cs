using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Vouchers;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Vouchers;

public sealed partial class EfVoucherStore : IVoucherStore
{
    private readonly IGarageContext _context;

    public EfVoucherStore(IGarageContext context)
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

    private async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await action(ct);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<(IReadOnlyList<Voucher> Items, int TotalCount, IReadOnlyList<string> AvailableStatuses)> GetAdminListPageAsync(
        bool includeDeleted,
        string? search,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var baseQuery = _context.Vouchers.AsNoTracking();
        if (!includeDeleted)
        {
            baseQuery = baseQuery.Where(v => v.IsDeleted != true);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(v =>
                (v.Code != null && v.Code.ToUpper().Contains(term)) ||
                (v.VoucherName != null && v.VoucherName.ToUpper().Contains(term)) ||
                (v.Type != null && v.Type.ToUpper().Contains(term)) ||
                (v.Description != null && v.Description.ToUpper().Contains(term)));
        }

        var availableStatuses = await baseQuery
            .Select(v => v.Status)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            baseQuery = baseQuery.Where(v => v.Status == normalizedStatus);
        }

        var totalCount = await baseQuery.CountAsync(ct);
        var vouchers = await baseQuery
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (vouchers, totalCount, availableStatuses);
    }

    public async Task<(IReadOnlyList<Voucher> Items, int TotalCount, IReadOnlyList<string> AvailableStatuses)> GetLegacyAdminListPageAsync(
        bool includeDeleted,
        string? search,
        string? normalizedStatus,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var all = _context.Vouchers.AsNoTracking();
        var statuses = await all.Select(x => x.Status.Trim().ToUpper()).Distinct().OrderBy(x => x)
            .ToListAsync(ct);

        var query = all;
        if (!includeDeleted) query = query.Where(x => x.IsDeleted != true);
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(x => x.Status.Trim().ToUpper() == normalizedStatus);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(x => (x.Code + " " + x.VoucherName + " " + x.Type + " " + (x.Description ?? "null"))
                .ToUpper().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var data = offset >= total
            ? new List<Voucher>()
            : await query.OrderBy(x => x.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (data, total, statuses);
    }

    public async Task<VoucherReportPageData> GetReportPageDataAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var all = _context.Vouchers.AsNoTracking();
        var total = await all.CountAsync(ct);
        var issued = await all.SumAsync(x => (long?)x.IssuedCount, ct) ?? 0;
        var redeemed = await all.SumAsync(x => (long?)x.RedeemedCount, ct) ?? 0;
        var budgetLimit = await all.SumAsync(x => x.BudgetLimit, ct) ?? 0m;
        var budgetUsed = await all.SumAsync(x => (decimal?)x.BudgetUsed, ct) ?? 0m;
        var offset = ((long)page - 1) * pageSize;
        var data = offset >= total
            ? new List<Voucher>()
            : await all.OrderByDescending(x => x.RedeemedCount).ThenBy(x => x.Id).Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return new VoucherReportPageData(data, total, issued, redeemed, budgetLimit, budgetUsed);
    }

    public async Task<VoucherDashboardData> GetDashboardDataAsync(CancellationToken ct = default)
    {
        var vouchers = _context.Vouchers.AsNoTracking();
        var activeCount = await vouchers.CountAsync(x => x.Status.Trim().ToUpper() == "ACTIVE" || x.IsActive == true, ct);
        var issued = await vouchers.SumAsync(x => (long?)x.IssuedCount, ct) ?? 0L;
        var redeemed = await vouchers.SumAsync(x => (long?)x.RedeemedCount, ct) ?? 0L;
        var budgetLimit = await vouchers.SumAsync(x => x.BudgetLimit, ct) ?? 0m;
        var budgetUsed = await vouchers.SumAsync(x => (decimal?)x.BudgetUsed, ct) ?? 0m;
        var totalDiscount = await _context.VoucherRedemptions.AsNoTracking()
            .SumAsync(x => (decimal?)x.DiscountAmount, ct) ?? 0m;
        var pending = _context.VoucherApprovalRequests.AsNoTracking().Where(x => x.Status.Trim().ToUpper() == "PENDING");
        var pendingApprovals = await pending.CountAsync(ct);
        var approvalItems = await pending.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(6).Select(x => new VoucherApprovalSummaryItem(x.Id, x.RequestType, x.RiskLevel)).ToListAsync(ct);
        var top = await vouchers.OrderByDescending(x => x.RedeemedCount).ThenBy(x => x.Id)
            .Take(8).ToListAsync(ct);
        var near = await vouchers.Where(x => x.IssuedCount > 0 && (decimal)x.RedeemedCount / x.IssuedCount >= 0.8m)
            .OrderByDescending(x => (decimal)x.RedeemedCount / x.IssuedCount).ThenBy(x => x.Id)
            .Take(8).Select(x => new VoucherNearQuotaItem(x.Id, x.Code, x.VoucherName, (decimal)x.RedeemedCount / x.IssuedCount))
            .ToListAsync(ct);

        return new VoucherDashboardData(
            activeCount,
            issued,
            redeemed,
            budgetLimit,
            budgetUsed,
            totalDiscount,
            pendingApprovals,
            top,
            near,
            approvalItems);
    }

    public Task<Voucher?> GetByIdAsync(long id, bool track = false, CancellationToken ct = default)
    {
        var q = track ? _context.Vouchers : _context.Vouchers.AsNoTracking();
        return q.FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);
    }

    public Task<Voucher?> GetByCodeAsync(string normalizedCode, CancellationToken ct = default)
    {
        return _context.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Code == normalizedCode && v.IsDeleted != true, ct);
    }

    public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken ct = default)
    {
        return _context.Vouchers
            .AsNoTracking()
            .AnyAsync(v => v.Code == normalizedCode && v.IsDeleted != true, ct);
    }

    public Task AddVoucherAsync(Voucher entity, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ =>
        {
            _context.Vouchers.Add(entity);
            return Task.CompletedTask;
        }, ct);
    }

    public Task SaveVoucherAsync(Voucher entity, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
    }

    public Task<int> CountCustomerRedeemedUsesAsync(long voucherId, long customerId, CancellationToken ct = default)
    {
        return _context.VoucherRedemptions
            .AsNoTracking()
            .CountAsync(r => r.VoucherId == voucherId && r.CustomerId == customerId && r.RedemptionStatus == "REDEEMED", ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }
}
