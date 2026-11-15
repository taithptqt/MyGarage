using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IInvoiceDetailRepository : IGenericRepository<InvoiceDetail>
    {
        Task<IReadOnlyList<InvoiceDetail>> GetScopedAsync(long? branchId, CancellationToken cancellationToken = default);
        Task<InvoiceDetail?> GetScopedAsync(long id, long? branchId, CancellationToken cancellationToken = default);
    }
}
