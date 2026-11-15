using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IDiagnosisSnapshotSignalQueryStore
{
    Task<IReadOnlyList<ServiceOrderSnapshot>> GetSnapshotsByServiceOrderNoTrackingAsync(long serviceOrderId);
    Task<ServiceOrderSnapshot?> GetSnapshotByIdAndOrderAsync(long snapshotId, long serviceOrderId);
    Task<IReadOnlyList<DiagnosisSnapshotRow>> GetSnapshotsInRangeNoTrackingAsync(DateTime utcStart, DateTime utcEnd);
    Task<IReadOnlyList<string?>> GetSnapshotDataListByServiceOrderNoTrackingAsync(long serviceOrderId);
    Task<int> GetMaxSnapshotRevisionAsync(long serviceOrderId);

    Task<int> CountServiceOrderDetailsAsync(long serviceOrderId);
    Task<IReadOnlyList<DiagnosisServiceSignalRow>> GetServiceSignalsForOrderAsync(long serviceOrderId);
    Task<IReadOnlyList<string?>> GetRequestedServiceCodesAsync(long serviceOrderId);
    Task<IReadOnlyList<DiagnosisStandardServiceDurationRow>> GetRequestedServiceDurationsAsync(long serviceOrderId);
    Task<IReadOnlyList<DiagnosisPackageCandidateRow>> GetActiveStandardServiceCandidatesAsync();
}
