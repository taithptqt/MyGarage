using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface ICategoryQueryService
{
    Task<(IReadOnlyList<Category> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
}
