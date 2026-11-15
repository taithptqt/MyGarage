using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public sealed record TechnicianProfileInfo(long Id, string FullName, string? Specialty);

public sealed record TechnicianProductivityQueryRow(
    long AssignmentId,
    long TechnicianId,
    string? TechnicianName,
    string? Specialty,
    long ServiceOrderDetailId,
    long? ServiceOrderId,
    string? ServiceOrderCode,
    string? ServiceName,
    DateTime WorkStartTime,
    DateTime WorkEndTime,
    int ActualMinutes,
    int FlatRateMinutes,
    decimal SplitRatio,
    decimal TaskRevenue);

public interface ITechnicianProductivityStore
{
    Task<TechnicianProfileInfo?> GetTechnicianProfileAsync(long technicianId);
    Task<IReadOnlyList<TechnicianProductivityQueryRow>> GetProductivityQueryRowsAsync(
        DateTime fromDateTime,
        DateTime toExclusive,
        long? technicianId,
        long? branchId);
}
