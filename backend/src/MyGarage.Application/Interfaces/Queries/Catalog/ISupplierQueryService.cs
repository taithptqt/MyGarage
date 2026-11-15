using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface ISupplierQueryService
{
    Task<(IReadOnlyList<Supplier> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
}
