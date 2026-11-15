using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IInvoiceRepository : IGenericRepository<Invoice>
    {
        Task<(IReadOnlyList<Invoice> Items, int Total)> GetScopedPageAsync(long? branchId, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<Invoice?> GetScopedAsync(long id, long? branchId, CancellationToken cancellationToken = default);
    }
}
