using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Repositories
{
    public class InvoiceDetailRepository : GenericRepository<InvoiceDetail>, IInvoiceDetailRepository
    {
        public InvoiceDetailRepository(IGarageContext context) : base(context)
        {
        }

        private IQueryable<InvoiceDetail> Scoped(long? branchId)
        {
            var query = _context.InvoiceDetails.AsNoTracking();
            return branchId.HasValue ? query.Where(x =>
                (x.Invoice.ServiceOrderId.HasValue && x.Invoice.ServiceOrder != null && x.Invoice.ServiceOrder.ExecuteBranchId == branchId.Value) ||
                (!x.Invoice.ServiceOrderId.HasValue && x.Invoice.RetailOrder != null && x.Invoice.RetailOrder.BranchId == branchId.Value)) : query;
        }

        public async Task<IReadOnlyList<InvoiceDetail>> GetScopedAsync(long? branchId, CancellationToken cancellationToken = default) =>
            await Scoped(branchId).OrderBy(x => x.Id).ToListAsync(cancellationToken);

        public Task<InvoiceDetail?> GetScopedAsync(long id, long? branchId, CancellationToken cancellationToken = default) =>
            Scoped(branchId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}

