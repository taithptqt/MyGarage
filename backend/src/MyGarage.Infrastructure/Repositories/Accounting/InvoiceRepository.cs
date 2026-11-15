using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Repositories
{
    public class InvoiceRepository : GenericRepository<Invoice>, IInvoiceRepository
    {
        private readonly IGarageContext _invoiceContext;
        public InvoiceRepository(IGarageContext context) : base(context)
        {
            _invoiceContext = context;
        }

        private IQueryable<Invoice> Scoped(long? branchId)
        {
            var query = _invoiceContext.Invoices.AsNoTracking();
            return branchId.HasValue
                ? query.Where(x => (x.ServiceOrderId.HasValue && x.ServiceOrder != null && x.ServiceOrder.ExecuteBranchId == branchId.Value) ||
                                   (!x.ServiceOrderId.HasValue && x.RetailOrder != null && x.RetailOrder.BranchId == branchId.Value))
                : query;
        }

        public async Task<(IReadOnlyList<Invoice> Items, int Total)> GetScopedPageAsync(long? branchId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = Scoped(branchId);
            var total = await query.CountAsync(cancellationToken);
            var offset = ((long)page - 1) * pageSize;
            if (offset >= total) return (Array.Empty<Invoice>(), total);
            var rows = await query.OrderByDescending(x => x.Id).Skip((int)offset).Take(pageSize).ToListAsync(cancellationToken);
            return (rows, total);
        }

        public Task<Invoice?> GetScopedAsync(long id, long? branchId, CancellationToken cancellationToken = default) =>
            Scoped(branchId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}

