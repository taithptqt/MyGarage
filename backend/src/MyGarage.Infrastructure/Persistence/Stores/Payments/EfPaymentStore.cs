using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Payments;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Infrastructure.Payments;

public sealed class EfPaymentStore : IPaymentStore
{
    private const string PaymentStatusCompleted = nameof(PaymentStatusCode.COMPLETED);
    private readonly IGarageContext _context;
    private readonly IOrderStore? _orderStore;

    public EfPaymentStore(IGarageContext context, IOrderStore? orderStore = null)
    {
        _context = context;
        _orderStore = orderStore;
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

    public Task<Payment?> GetPaymentByIdAsync(long id, bool track, CancellationToken ct = default)
    {
        var query = track ? _context.Payments.AsQueryable() : _context.Payments.AsNoTracking();
        return query.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public Task<long?> GetBranchIdForServiceOrderAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return _context.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => (long?)so.ExecuteBranchId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<long?> GetBranchIdForRetailOrderAsync(long retailOrderId, CancellationToken ct = default)
    {
        return _context.RetailOrders
            .AsNoTracking()
            .Where(ro => ro.Id == retailOrderId)
            .Select(ro => (long?)ro.BranchId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddPaymentAsync(Payment payment, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync(ct);

                if (tx != null)
                {
                    await tx.CommitAsync(ct);
                }
            }
            catch
            {
                if (tx != null)
                {
                    await tx.RollbackAsync(ct);
                }
                throw;
            }
        });
    }

    public async Task SaveChangesInTransactionAsync(CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync(ct);

                if (tx != null)
                {
                    await tx.CommitAsync(ct);
                }
            }
            catch
            {
                if (tx != null)
                {
                    await tx.RollbackAsync(ct);
                }
                throw;
            }
        });
    }

    public async Task<IReadOnlyList<Payment>> GetCustomerPaymentsAsync(long customerId, CancellationToken ct = default)
    {
        return await _context.Payments
            .AsNoTracking()
            .Where(p => p.PayerId == customerId && p.PayerType == "CUSTOMER")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<PaymentAuthInfo?> GetPaymentAuthInfoAsync(long paymentId, CancellationToken ct = default)
    {
        return _context.Payments
            .AsNoTracking()
            .Where(p => p.Id == paymentId)
            .Select(p => new PaymentAuthInfo(p.Id, p.PayerId, p.PayerType, p.ServiceOrderId, p.RetailOrderId, p.InvoiceId))
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> IsServiceOrderOwnedByCustomerAsync(long serviceOrderId, long customerId, CancellationToken ct = default)
    {
        return _context.ServiceOrders
            .AsNoTracking()
            .AnyAsync(so => so.Id == serviceOrderId && so.CustomerId == customerId, ct);
    }

    public Task<bool> IsRetailOrderOwnedByCustomerAsync(long retailOrderId, long customerId, CancellationToken ct = default)
    {
        return _context.RetailOrders
            .AsNoTracking()
            .AnyAsync(ro => ro.Id == retailOrderId && ro.CustomerId == customerId, ct);
    }

    public Task<Invoice?> GetInvoiceForUpdateAsync(long invoiceId, CancellationToken ct = default)
    {
        return _context.Invoices.FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
    }

    public async Task<decimal> GetCompletedPaymentsSumForInvoiceAsync(long invoiceId, CancellationToken ct = default)
    {
        return await _context.Payments
            .Where(x => x.InvoiceId == invoiceId && x.Status == PaymentStatusCompleted)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
    }

    public Task<ServiceOrder?> GetServiceOrderForUpdateAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId, ct);
    }

    public async Task<decimal> GetCompletedPaymentsSumForServiceOrderAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return await _context.Payments
            .Where(x => x.ServiceOrderId == serviceOrderId && x.Status == PaymentStatusCompleted)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
    }

    public Task<RetailOrder?> GetRetailOrderForUpdateAsync(long retailOrderId, CancellationToken ct = default)
    {
        return _context.RetailOrders.FirstOrDefaultAsync(x => x.Id == retailOrderId, ct);
    }

    public async Task<decimal> GetCompletedPaymentsSumForRetailOrderAsync(long retailOrderId, CancellationToken ct = default)
    {
        return await _context.Payments
            .Where(x => x.RetailOrderId == retailOrderId && x.Status == PaymentStatusCompleted)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
    }

    public Task EnsureRetailOrderStockDeductedAsync(long retailOrderId, CancellationToken ct = default)
    {
        return RetailOrderStockCommitter.EnsureStockDeductedAsync(_context, retailOrderId, ct);
    }

    public async Task<CustomerEmailInfo?> GetCustomerEmailInfoAsync(long customerId, CancellationToken ct = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new CustomerEmailInfo(c.Email, c.FullName))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CustomerEmailInfo?> GetServiceOrderCustomerEmailInfoAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return await _context.ServiceOrders
            .AsNoTracking()
            .Where(s => s.Id == serviceOrderId && s.Customer != null)
            .Select(s => new CustomerEmailInfo(s.Customer!.Email, s.Customer.FullName))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CustomerEmailInfo?> GetRetailOrderRecipientInfoAsync(long retailOrderId, CancellationToken ct = default)
    {
        var retailOrder = await _context.RetailOrders
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Invoices)
            .FirstOrDefaultAsync(r => r.Id == retailOrderId, ct);

        if (retailOrder is null)
        {
            return null;
        }

        if (retailOrder.Customer is not null && !string.IsNullOrWhiteSpace(retailOrder.Customer.Email))
        {
            return new CustomerEmailInfo(retailOrder.Customer.Email, retailOrder.Customer.FullName);
        }

        var invoiceReceiveEmail = retailOrder.Invoices
            .OrderByDescending(i => i.IssuedAt ?? i.CreatedAt ?? DateTime.MinValue)
            .Select(i => i.ReceiveEmail)
            .FirstOrDefault(email => !string.IsNullOrWhiteSpace(email));

        if (!string.IsNullOrWhiteSpace(invoiceReceiveEmail))
        {
            return new CustomerEmailInfo(invoiceReceiveEmail, retailOrder.Customer?.FullName);
        }

        return null;
    }

    public OrderBuyerInfo? GetOrderBuyerInfoByPaymentLinkId(string paymentLinkId)
    {
        var order = _orderStore?.GetOrderByPaymentLinkId(paymentLinkId);
        return order is null ? null : new OrderBuyerInfo(order.BuyerEmail, order.BuyerName);
    }

    public Task<InvoiceEmailInfo?> GetInvoiceEmailInfoAsync(long invoiceId, CancellationToken ct = default)
    {
        return _context.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new InvoiceEmailInfo(i.PayerType, i.PayerId, i.ReceiveEmail))
            .FirstOrDefaultAsync(ct);
    }

    public Task<long?> GetRetailOrderCustomerIdAsync(long retailOrderId, CancellationToken ct = default)
    {
        return _context.RetailOrders
            .AsNoTracking()
            .Where(r => r.Id == retailOrderId && r.CustomerId.HasValue)
            .Select(r => r.CustomerId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<RetailOrder?> GetRetailOrderWithItemsForEmailAsync(long retailOrderId, CancellationToken ct = default)
    {
        return _context.RetailOrders
            .AsNoTracking()
            .Include(r => r.RetailOrderItems)
            .ThenInclude(ri => ri.Product)
            .FirstOrDefaultAsync(r => r.Id == retailOrderId, ct);
    }
}
