using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public sealed record VehicleHistoryHeaderRecord(long Id, string? LicensePlate, string? Vin);

public sealed record VehicleHistoryOrderRecord(
    long Id,
    string? Code,
    string? Status,
    DateTime? CheckInTime,
    DateTime? ActualHandover,
    decimal? FinalAmount,
    string? PaymentStatus,
    string? QcStatus);

public sealed record VehicleHistoryDetailRecord(long ServiceOrderId, string ItemType, string ItemName);

public interface IVehicleHistoryStore
{
    Task<VehicleHistoryHeaderRecord?> GetVehicleAsync(long vehicleId);
    Task<(int TotalCount, IReadOnlyList<VehicleHistoryOrderRecord> Orders)> GetPagedServiceOrdersAsync(long vehicleId, int page, int pageSize);
    Task<IReadOnlyList<VehicleHistoryDetailRecord>> GetOrderDetailsAsync(IReadOnlyCollection<long> orderIds);
    Task<IReadOnlyDictionary<long, int>> GetDiagnosisCountsAsync(IReadOnlyCollection<long> orderIds);
    Task<IReadOnlyDictionary<long, decimal>> GetPaymentSumsAsync(IReadOnlyCollection<long> orderIds);
}
