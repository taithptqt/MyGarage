using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed partial class EfDiagnosisStore
{
    private const string DiagnosisStatusPending = nameof(DiagnosisStatus.PENDING);
    private const string DiagnosisStatusAssigned = nameof(DiagnosisStatus.ASSIGNED);
    private const string DiagnosisStatusCompleted = nameof(DiagnosisStatus.COMPLETED);
    private const string DiagnosisStatusCanceled = nameof(DiagnosisStatus.CANCELED);

    public async Task<IReadOnlyList<DiagnosisAssignment>> GetDiagnosisAssignmentsForDiagnosisAsync(long diagnosisId)
    {
        return await _context.DiagnosisAssignments.AsNoTracking()
            .Where(x => x.DiagnosisId == diagnosisId)
            .OrderBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DiagnosisAssignment>> GetDiagnosisAssignmentsByDiagnosisIdsAsync(IReadOnlyCollection<long> diagnosisIds)
    {
        if (diagnosisIds.Count == 0)
        {
            return Array.Empty<DiagnosisAssignment>();
        }

        return await _context.DiagnosisAssignments.AsNoTracking()
            .Where(x => diagnosisIds.Contains(x.DiagnosisId))
            .OrderBy(x => x.DiagnosisId)
            .ThenBy(x => x.Id)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DiagnosisAssignment>> GetDiagnosisAssignmentsByIdsAndDiagnosisIdsAsync(
        IReadOnlyCollection<long> assignmentIds,
        IReadOnlyCollection<long> diagnosisIds)
    {
        if (assignmentIds.Count == 0 || diagnosisIds.Count == 0)
        {
            return Array.Empty<DiagnosisAssignment>();
        }

        return await _context.DiagnosisAssignments.AsNoTracking()
            .Where(x => assignmentIds.Contains(x.Id))
            .Where(x => diagnosisIds.Contains(x.DiagnosisId))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<long>> GetAssignmentIdsByDiagnosisIdsAsync(IReadOnlyCollection<long> diagnosisIds)
    {
        if (diagnosisIds.Count == 0)
        {
            return Array.Empty<long>();
        }

        return await _context.DiagnosisAssignments.AsNoTracking()
            .Where(x => diagnosisIds.Contains(x.DiagnosisId))
            .Select(x => x.Id)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DiagnosisRecommendation>> GetActiveRecommendationsForAssignmentAsync(long diagnosisId, long assignmentId)
    {
        return await _context.DiagnosisRecommendations.AsNoTracking()
            .Where(x => x.DiagnosisId == diagnosisId && x.DiagnosisAssignmentId == assignmentId)
            .Where(x => !string.Equals(x.RecommendationStatus, nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.ParentRecommendationId.HasValue ? 1 : 0)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DiagnosisRecommendation>> GetRecommendationsForRemovalAsync(
        IReadOnlyCollection<long> diagnosisIds,
        IReadOnlyCollection<long> assignmentIds)
    {
        if (diagnosisIds.Count == 0 && assignmentIds.Count == 0)
        {
            return Array.Empty<DiagnosisRecommendation>();
        }

        return await _context.DiagnosisRecommendations
            .Where(x => diagnosisIds.Contains(x.DiagnosisId) ||
                        (x.DiagnosisAssignmentId.HasValue && assignmentIds.Contains(x.DiagnosisAssignmentId.Value)))
            .ToListAsync();
    }

    public Task<Diagnosis?> GetDiagnosisByIdAsync(long id)
    {
        return _context.Diagnoses.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IReadOnlyList<Diagnosis>> GetFilteredDiagnosesNoTrackingAsync(long? serviceOrderId, bool requestOnly)
    {
        var query = _context.Diagnoses.AsNoTracking().AsQueryable();

        if (serviceOrderId.HasValue)
        {
            query = query.Where(x => x.ServiceOrderId == serviceOrderId.Value);
        }

        if (requestOnly)
        {
            query = query.Where(x =>
                x.Status == DiagnosisStatusPending ||
                x.Status == DiagnosisStatusAssigned);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Diagnosis>> GetDiagnosesByServiceOrderAsync(long serviceOrderId)
    {
        return await _context.Diagnoses
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Diagnosis>> GetActiveDiagnosesByServiceOrderAsync(long serviceOrderId)
    {
        return await _context.Diagnoses
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Where(x =>
                x.Status == DiagnosisStatusPending ||
                x.Status == DiagnosisStatusAssigned)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Diagnosis>> GetDispatchBoardDiagnosesNoTrackingAsync(bool includeCompleted)
    {
        var query = _context.Diagnoses.AsNoTracking().AsQueryable();

        if (!includeCompleted)
        {
            query = query.Where(x => x.Status != DiagnosisStatusCompleted && x.Status != DiagnosisStatusCanceled);
        }

        return await query.ToListAsync();
    }

    public async Task<IReadOnlyList<string?>> GetDiagnosisStatusesByServiceOrderAsync(long serviceOrderId)
    {
        return await _context.Diagnoses
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Select(x => x.Status)
            .ToListAsync();
    }

    public Task<ServiceOrder?> GetServiceOrderByIdAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
    }

    public Task<bool> HasSignedCheckInAsync(long serviceOrderId, string logTypeCheckIn)
    {
        return _context.VehicleConditionLogs
            .AnyAsync(x =>
                x.ServiceOrderId == serviceOrderId &&
                x.LogType == logTypeCheckIn &&
                x.CustomerSignatureUrl != null &&
                x.CustomerSignatureUrl != "");
    }

    public async Task<IReadOnlyList<DiagnosisServiceOrderBoardRow>> GetServiceOrderBoardRowsAsync(IReadOnlyCollection<long> serviceOrderIds, long? branchId)
    {
        if (serviceOrderIds.Count == 0)
        {
            return Array.Empty<DiagnosisServiceOrderBoardRow>();
        }

        var query = _context.ServiceOrders.AsNoTracking()
            .Where(x => serviceOrderIds.Contains(x.Id));

        if (branchId.HasValue)
        {
            query = query.Where(x => x.ExecuteBranchId == branchId.Value);
        }

        return await query
            .Select(x => new DiagnosisServiceOrderBoardRow(x.Id, x.Code, x.ExecuteBranchId, x.Status))
            .ToListAsync();
    }

    public Task<DiagnosisDispatchServiceOrderInfo?> GetDispatchServiceOrderInfoAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.AsNoTracking()
            .Where(x => x.Id == serviceOrderId)
            .Select(x => new DiagnosisDispatchServiceOrderInfo(x.Id, x.ExecuteBranchId, x.OrderCategory, x.Status))
            .FirstOrDefaultAsync();
    }

    public Task<DiagnosisIntakeServiceOrderInfo?> GetIntakeServiceOrderInfoAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.AsNoTracking()
            .Where(x => x.Id == serviceOrderId)
            .Select(x => new DiagnosisIntakeServiceOrderInfo(
                x.Id,
                x.ExecuteBranchId,
                x.VehicleId,
                x.OrderCategory,
                x.CustomerNotes,
                x.CheckInTime,
                x.CreatedAt))
            .FirstOrDefaultAsync();
    }

    public Task<DiagnosisQueueServiceOrderInfo?> GetQueueServiceOrderInfoAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.AsNoTracking()
            .Where(x => x.Id == serviceOrderId)
            .Select(x => new DiagnosisQueueServiceOrderInfo(
                x.Id,
                x.ExecuteBranchId,
                x.CustomerId,
                x.VehicleId,
                x.AppointmentId,
                x.CheckInTime,
                x.CreatedAt))
            .FirstOrDefaultAsync();
    }

    public Task<string?> GetServiceOrderCategoryAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.AsNoTracking()
            .Where(x => x.Id == serviceOrderId)
            .Select(x => x.OrderCategory)
            .FirstOrDefaultAsync();
    }

    public async Task<DateTime?> GetLastVehicleVisitAtAsync(long vehicleId, long excludeServiceOrderId)
    {
        return await _context.ServiceOrders.AsNoTracking()
            .Where(x => x.VehicleId == vehicleId && x.Id != excludeServiceOrderId)
            .Select(x => x.CheckInTime ?? x.CreatedAt)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .OrderByDescending(x => x)
            .FirstOrDefaultAsync();
    }

    public Task<bool> HasRecentReworkOrWarrantyAsync(long vehicleId, long excludeServiceOrderId, DateTime since)
    {
        return _context.ServiceOrders.AsNoTracking()
            .Where(x => x.VehicleId == vehicleId && x.Id != excludeServiceOrderId)
            .Where(x =>
                x.OrderCategory == "REWORK" ||
                x.OrderCategory == "WARRANTLY" ||
                x.OrderCategory == "WARRANTY")
            .Where(x => (x.CheckInTime ?? x.CreatedAt) >= since)
            .AnyAsync();
    }

    public async Task<IReadOnlyDictionary<long, long>> GetServiceOrderExecuteBranchMapAsync(IReadOnlyCollection<long> orderIds)
    {
        if (orderIds.Count == 0)
        {
            return new Dictionary<long, long>();
        }

        return await _context.ServiceOrders.AsNoTracking()
            .Where(x => orderIds.Contains(x.Id))
            .Select(x => new { x.Id, x.ExecuteBranchId })
            .ToDictionaryAsync(x => x.Id, x => x.ExecuteBranchId);
    }

    public async Task<IReadOnlyList<ServiceOrderSnapshot>> GetSnapshotsByServiceOrderNoTrackingAsync(long serviceOrderId)
    {
        return await _context.ServiceOrderSnapshots.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderByDescending(x => x.RevisionNumber)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public Task<ServiceOrderSnapshot?> GetSnapshotByIdAndOrderAsync(long snapshotId, long serviceOrderId)
    {
        return _context.ServiceOrderSnapshots
            .FirstOrDefaultAsync(x => x.Id == snapshotId && x.ServiceOrderId == serviceOrderId);
    }

    public async Task<IReadOnlyList<DiagnosisSnapshotRow>> GetSnapshotsInRangeNoTrackingAsync(DateTime utcStart, DateTime utcEnd)
    {
        return await _context.ServiceOrderSnapshots.AsNoTracking()
            .Where(x => (x.CreatedAt ?? DateTime.MinValue) >= utcStart && (x.CreatedAt ?? DateTime.MinValue) <= utcEnd)
            .Select(x => new DiagnosisSnapshotRow(x.Id, x.ServiceOrderId, x.RevisionNumber, x.CreatedAt, x.SnapshotData))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string?>> GetSnapshotDataListByServiceOrderNoTrackingAsync(long serviceOrderId)
    {
        return await _context.ServiceOrderSnapshots.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .OrderByDescending(x => x.RevisionNumber)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => x.SnapshotData)
            .ToListAsync();
    }

    public async Task<int> GetMaxSnapshotRevisionAsync(long serviceOrderId)
    {
        return await _context.ServiceOrderSnapshots.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Select(x => (int?)x.RevisionNumber)
            .MaxAsync() ?? 0;
    }

    public Task<int> CountServiceOrderDetailsAsync(long serviceOrderId)
    {
        return _context.ServiceOrderDetails.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .CountAsync();
    }

    public async Task<IReadOnlyList<DiagnosisServiceSignalRow>> GetServiceSignalsForOrderAsync(long serviceOrderId)
    {
        return await _context.ServiceOrderDetails.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId && x.StandardServiceId.HasValue)
            .Join(
                _context.StandardServices.AsNoTracking(),
                detail => detail.StandardServiceId!.Value,
                standard => standard.Id,
                (detail, standard) => new DiagnosisServiceSignalRow(standard.Code, standard.Name))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string?>> GetRequestedServiceCodesAsync(long serviceOrderId)
    {
        return await _context.ServiceOrderDetails.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId && x.StandardServiceId.HasValue)
            .Join(
                _context.StandardServices.AsNoTracking(),
                detail => detail.StandardServiceId!.Value,
                standard => standard.Id,
                (detail, standard) => standard.Code)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DiagnosisStandardServiceDurationRow>> GetRequestedServiceDurationsAsync(long serviceOrderId)
    {
        return await _context.ServiceOrderDetails.AsNoTracking()
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Where(x => x.StandardServiceId.HasValue)
            .Join(
                _context.StandardServices.AsNoTracking(),
                detail => detail.StandardServiceId!.Value,
                standard => standard.Id,
                (detail, standard) => new DiagnosisStandardServiceDurationRow(standard.StandardMinutes, detail.Quantity))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DiagnosisPackageCandidateRow>> GetActiveStandardServiceCandidatesAsync()
    {
        return await _context.StandardServices.AsNoTracking()
            .Where(service => service.IsDeleted != true && service.IsActive != false)
            .Select(service => new DiagnosisPackageCandidateRow(
                service.Id,
                service.Code,
                service.Name,
                service.StandardMinutes))
            .ToListAsync();
    }
}
