namespace MyGarage.Application.Interfaces.Queries;

public sealed record InspectionServiceRow(
    string? Code,
    string? Name,
    string? CategoryCode,
    string? CategoryName);

public sealed record ServiceOrderDetailTextRow(string? ItemName, string? Description);

public interface IVehicleConditionLogQueryService
{
    Task<IReadOnlyList<InspectionServiceRow>> GetInspectionServiceRowsAsync(
        IReadOnlyCollection<long> requestedServiceIds,
        CancellationToken ct = default);

    Task<IReadOnlyList<ServiceOrderDetailTextRow>> GetServiceOrderDetailTextsAsync(
        long serviceOrderId,
        CancellationToken ct = default);

    Task<IReadOnlyList<long>> LoadRequestedServiceIdsFromServiceOrderAsync(
        long serviceOrderId,
        CancellationToken ct = default);

    Task<string?> GetAppointmentRequestedServiceIdsAsync(long appointmentId, CancellationToken ct = default);
}
