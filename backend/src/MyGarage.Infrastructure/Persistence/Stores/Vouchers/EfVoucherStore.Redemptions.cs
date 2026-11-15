using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Vouchers;

public sealed partial class EfVoucherStore
{
    public async Task<TResult> ExecuteVoucherTransactionAsync<TResult>(
        string rejectionMessageWhenInTransaction,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            throw new InvalidOperationException(rejectionMessageWhenInTransaction);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                var result = await action(ct);
                if (tx != null)
                {
                    await tx.CommitAsync(ct);
                }
                return result;
            }
            catch
            {
                if (tx != null)
                {
                    try { await tx.RollbackAsync(ct); } catch { /* Preserve original failure. */ }
                }
                throw;
            }
        });
    }

    // All voucher writers acquire the same transaction-owned SQL lock.
    // Rowversion remains a second guard against writers outside this service.
    public async Task AcquireVoucherOperationLockAsync(string name, CancellationToken ct = default)
    {
        if (!_context.Database.IsSqlServer()) return;
        var resource = "MyGarage:Voucher:" + name;
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @result int;
EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive',
    @LockOwner='Transaction', @LockTimeout=10000;
IF @result < 0 THROW 51001, 'Could not acquire voucher operation lock.', 1;", ct);
    }

    public Task<long?> FindVoucherIdAsync(long? voucherId, string? voucherCode, CancellationToken ct = default)
    {
        return _context.Vouchers
            .AsNoTracking()
            .Where(v => voucherId.HasValue ? v.Id == voucherId.Value : v.Code == (voucherCode ?? string.Empty).Trim())
            .Select(v => (long?)v.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<Voucher> GetVoucherByIdRequiredAsync(long id, CancellationToken ct = default)
    {
        return _context.Vouchers.FirstAsync(x => x.Id == id, ct);
    }

    public async Task AddRedemptionAndSaveAsync(VoucherRedemption redemption, CancellationToken ct = default)
    {
        _context.VoucherRedemptions.Add(redemption);
        await _context.SaveChangesAsync(ct);
    }

    public Task<long?> GetRedemptionVoucherIdAsync(long redemptionId, CancellationToken ct = default)
    {
        return _context.VoucherRedemptions
            .AsNoTracking()
            .Where(x => x.Id == redemptionId)
            .Select(x => (long?)x.VoucherId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<VoucherRedemption?> GetRedemptionWithVoucherForUpdateAsync(long redemptionId, CancellationToken ct = default)
    {
        return _context.VoucherRedemptions
            .Include(x => x.Voucher)
            .FirstOrDefaultAsync(x => x.Id == redemptionId, ct);
    }

    public Task<Voucher?> ResolveVoucherForValidationAsync(long? voucherId, string? voucherCode, CancellationToken ct = default)
    {
        var query = _context.Vouchers.AsQueryable();

        if (voucherId.HasValue)
        {
            return query.FirstOrDefaultAsync(x => x.Id == voucherId.Value, ct);
        }

        if (!string.IsNullOrWhiteSpace(voucherCode))
        {
            var normalizedCode = voucherCode.Trim();
            return query.FirstOrDefaultAsync(x => x.Code == normalizedCode, ct);
        }

        return Task.FromResult<Voucher?>(null);
    }

    public Task<Invoice?> GetInvoiceByIdAsync(long invoiceId, bool track = false, CancellationToken ct = default)
    {
        var query = track ? _context.Invoices : _context.Invoices.AsNoTracking();
        return query.FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
    }

    public async Task<IReadOnlyList<VoucherAssignment>> GetBranchAssignmentsAsync(long voucherId, CancellationToken ct = default)
    {
        return await _context.VoucherAssignments
            .Where(x => x.VoucherId == voucherId && x.AssignmentType == "BRANCH")
            .ToListAsync(ct);
    }

    public async Task<string?> GetPaymentMethodCodeAsync(long paymentMethodId, CancellationToken ct = default)
    {
        var paymentMethod = await _context.PaymentMethods
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentMethodId, ct);
        return paymentMethod?.MethodCode;
    }

    public Task<int> CountVehicleRedeemedUsesAsync(long voucherId, long vehicleId, CancellationToken ct = default)
    {
        return _context.VoucherRedemptions
            .AsNoTracking()
            .CountAsync(x => x.VoucherId == voucherId
                && x.VehicleId == vehicleId
                && x.RedemptionStatus == "REDEEMED", ct);
    }

    public async Task<bool> HasCustomerPriorOrderAsync(long customerId, long? excludeServiceOrderId, long? excludeRetailOrderId, CancellationToken ct = default)
    {
        var hasPriorServiceOrder = await _context.ServiceOrders.AnyAsync(x =>
            x.CustomerId == customerId &&
            (!excludeServiceOrderId.HasValue || x.Id != excludeServiceOrderId.Value), ct);

        if (hasPriorServiceOrder)
        {
            return true;
        }

        return await _context.RetailOrders.AnyAsync(x =>
            x.CustomerId == customerId &&
            (!excludeRetailOrderId.HasValue || x.Id != excludeRetailOrderId.Value), ct);
    }
}
