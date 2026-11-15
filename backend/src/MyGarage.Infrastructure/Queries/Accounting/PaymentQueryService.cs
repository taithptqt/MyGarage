using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfPaymentQueryService : IPaymentQueryService
{
    private readonly IGarageContext _context;

    public EfPaymentQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<long?> GetServiceOrderExecuteBranchIdAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return await _context.ServiceOrders
            .AsNoTracking()
            .Where(so => so.Id == serviceOrderId)
            .Select(so => (long?)so.ExecuteBranchId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<long?> GetInvoiceServiceOrderIdAsync(long invoiceId, CancellationToken ct = default)
    {
        return await _context.Invoices
            .AsNoTracking()
            .Where(x => x.Id == invoiceId)
            .Select(x => x.ServiceOrderId)
            .FirstOrDefaultAsync(ct);
    }
}
