using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record ReceptionAppointmentListResult(
    IReadOnlyList<Appointment> Entities,
    int SingleCount,
    int GroupCount,
    IReadOnlyList<string> Statuses);

public interface IReceptionAppointmentListQueryService
{
    Task<ReceptionAppointmentListResult> GetAsync(
        long? scopedBranchId,
        string? search,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? sort,
        string? mode,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
