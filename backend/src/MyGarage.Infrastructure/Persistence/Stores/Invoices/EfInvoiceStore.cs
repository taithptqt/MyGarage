using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Invoices;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Invoices;

public sealed class EfInvoiceStore : IInvoiceStore
{
    private readonly IGarageContext _context;

    public EfInvoiceStore(IGarageContext context)
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

    public Task<Invoice?> GetInvoiceWithDetailsAsync(long id, CancellationToken ct = default)
    {
        return _context.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceDetails)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
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

    public Task<long?> GetServiceOrderIdByInvoiceIdAsync(long invoiceId, CancellationToken ct = default)
    {
        return _context.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.Id == invoiceId)
            .Select(invoice => invoice.ServiceOrderId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task CreateInvoiceWithDetailsAsync(
        Invoice invoice,
        IReadOnlyList<InvoiceDetail> details,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync(ct);

                foreach (var detail in details)
                {
                    detail.InvoiceId = invoice.Id;
                }
                _context.InvoiceDetails.AddRange(details);
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

    public Task<InvoiceAuthInfo?> GetInvoiceAuthInfoAsync(long invoiceId, CancellationToken ct = default)
    {
        return _context.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => new InvoiceAuthInfo(i.Id, i.PayerId, i.PayerType, i.ServiceOrderId, i.RetailOrderId))
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

    public Task<List<Payment>> GetCompletedPaymentsForBackfillAsync(CancellationToken ct = default)
    {
        return _context.Payments
            .Where(payment =>
                payment.Status != null &&
                payment.Status.ToUpper() == nameof(PaymentStatusCode.COMPLETED) &&
                ((payment.ServiceOrderId.HasValue && payment.ServiceOrderId.Value > 0) ||
                 (payment.RetailOrderId.HasValue && payment.RetailOrderId.Value > 0)))
            .OrderBy(payment => payment.Id)
            .ToListAsync(ct);
    }

    public Task<List<long>> GetAllInvoiceIdsAsync(CancellationToken ct = default)
    {
        return _context.Invoices
            .AsNoTracking()
            .Select(invoice => invoice.Id)
            .ToListAsync(ct);
    }

    public Task<Invoice?> GetLatestInvoiceByServiceOrderIdAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return _context.Invoices
            .Include(x => x.InvoiceDetails)
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<Invoice?> GetLatestInvoiceByRetailOrderIdAsync(long retailOrderId, CancellationToken ct = default)
    {
        return _context.Invoices
            .Include(x => x.InvoiceDetails)
            .Where(x => x.RetailOrderId == retailOrderId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<ServiceOrder?> GetServiceOrderWithDetailsForInvoiceAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return _context.ServiceOrders
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .Include(x => x.ServiceOrderDetails)
            .FirstOrDefaultAsync(x => x.Id == serviceOrderId, ct);
    }

    public Task<RetailOrder?> GetRetailOrderWithItemsForInvoiceAsync(long retailOrderId, CancellationToken ct = default)
    {
        return _context.RetailOrders
            .Include(x => x.Customer)
            .Include(x => x.RetailOrderItems)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == retailOrderId, ct);
    }

    public async Task AddInvoiceAndSaveAsync(Invoice invoice, CancellationToken ct = default)
    {
        _context.Invoices.Add(invoice);
        await SaveChangesInTransactionAsync(ct);
    }

    public async Task AddInvoiceDetailsAndSaveAsync(IEnumerable<InvoiceDetail> details, CancellationToken ct = default)
    {
        _context.InvoiceDetails.AddRange(details);
        await SaveChangesInTransactionAsync(ct);
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
}
