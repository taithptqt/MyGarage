using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IQcInspectionDataService
{
    Task<(IReadOnlyList<QcInspection> Items, int Total)> GetPagedByServiceOrderAsync(
        long serviceOrderId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<long?> GetServiceOrderIdAsync(long inspectionId, CancellationToken ct = default);
    Task<bool> IsTechnicianWhoWorkedOnOrderAsync(long serviceOrderId, long staffId, CancellationToken ct = default);
    Task<object?> UpsertChecklistItemsAsync(
        long inspectionId,
        bool overwriteExisting,
        IReadOnlyList<(QcInspectionChecklistItemUpsertDto Input, string ItemName, string? NormalizedResult)> validatedItems,
        CancellationToken ct = default);
}
