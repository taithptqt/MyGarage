using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface ICustomerFeedbackQueryService
{
    Task<(IReadOnlyList<CustomerFeedback> Items, int Total, int Page, int PageSize)> GetPagedAsync(
        long? customerId,
        int? page,
        int? pageSize,
        CancellationToken ct = default);
}
