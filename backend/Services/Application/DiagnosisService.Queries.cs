using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class DiagnosisService
    {
        public async Task<IReadOnlyList<Diagnosis>> GetAllFilteredAsync(long? serviceOrderId, bool requestOnly, long? teamId = null)
        {
            var query = _context.Diagnoses.AsNoTracking().AsQueryable();

            if (serviceOrderId.HasValue)
            {
                query = query.Where(x => x.ServiceOrderId == serviceOrderId.Value);
            }

            if (requestOnly)
            {
                query = query.Where(x => x.Status == DiagnosisStatusPending || x.Status == DiagnosisStatusAssigned);
            }

            if (teamId.HasValue)
            {
                var normalizedTeamTag = NormalizeDispatchTag(await _context.Teams
                    .Where(x => x.Id == teamId.Value)
                    .Select(x => x.SpecialtyTag)
                    .FirstOrDefaultAsync());

                query = query.Where(x =>
                    x.AssignedTeamId == teamId.Value ||
                    (
                        x.AssignedTeamId == null &&
                        x.Status == DiagnosisStatusPending &&
                        (
                            normalizedTeamTag == null ||
                            x.DispatchTag == null ||
                            x.DispatchTag == normalizedTeamTag
                        )
                    ));
            }

            return await query
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();
        }

        public async Task<ServiceOrder?> GetServiceOrderAsync(long serviceOrderId)
        {
            return await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
        }

        public async Task<bool> HasSignedCheckInAsync(long serviceOrderId)
        {
            return await _context.VehicleConditionLogs
                .AnyAsync(x =>
                    x.ServiceOrderId == serviceOrderId &&
                    x.LogType == "CHECK_IN" &&
                    x.CustomerSignatureUrl != null &&
                    x.CustomerSignatureUrl != "");
        }

        public async Task<IReadOnlyList<Diagnosis>> GetAllByServiceOrderAsync(long serviceOrderId)
        {
            return await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<DiagnosisDispatchBoardItemDto>> GetDispatchBoardAsync(long? branchId, long? teamId, bool includeCompleted)
        {
            var query = _context.Diagnoses.AsNoTracking().AsQueryable();

            if (!includeCompleted)
            {
                query = query.Where(x => x.Status != DiagnosisStatusCompleted);
            }

            if (teamId.HasValue)
            {
                var normalizedTeamTag = NormalizeDispatchTag(await _context.Teams
                    .Where(x => x.Id == teamId.Value)
                    .Select(x => x.SpecialtyTag)
                    .FirstOrDefaultAsync());

                query = query.Where(x =>
                    x.AssignedTeamId == teamId.Value ||
                    (
                        x.AssignedTeamId == null &&
                        x.Status == DiagnosisStatusPending &&
                        (
                            normalizedTeamTag == null ||
                            x.DispatchTag == null ||
                            x.DispatchTag == normalizedTeamTag
                        )
                    ));
            }

            var diagnoses = await query.ToListAsync();
            if (diagnoses.Count == 0)
            {
                return Array.Empty<DiagnosisDispatchBoardItemDto>();
            }

            var serviceOrderIds = diagnoses.Select(x => x.ServiceOrderId).Distinct().ToList();
            var serviceOrderQuery = _context.ServiceOrders.AsNoTracking()
                .Where(x => serviceOrderIds.Contains(x.Id));

            if (branchId.HasValue)
            {
                serviceOrderQuery = serviceOrderQuery.Where(x => x.ExecuteBranchId == branchId.Value);
            }

            var orderRows = await serviceOrderQuery
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.ExecuteBranchId,
                    x.Status,
                })
                .ToListAsync();

            if (orderRows.Count == 0)
            {
                return Array.Empty<DiagnosisDispatchBoardItemDto>();
            }

            var orderIds = orderRows.Select(x => x.Id).ToHashSet();
            diagnoses = diagnoses.Where(x => orderIds.Contains(x.ServiceOrderId)).ToList();

            var teamIds = diagnoses
                .Where(x => x.AssignedTeamId.HasValue)
                .Select(x => x.AssignedTeamId!.Value)
                .Distinct()
                .ToList();
            var teamNameMap = teamIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.Teams.AsNoTracking()
                    .Where(x => teamIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name })
                    .ToDictionaryAsync(x => x.Id, x => x.Name ?? string.Empty);

            var technicianIds = diagnoses
                .SelectMany(x => new long?[]
                {
                    x.TechnicianId,
                    x.AssistantTechnicianId,
                    x.ApprovedByForemanId,
                    x.FinalApprovedByManagerId,
                })
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
            var technicianNameMap = technicianIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.Staffs.AsNoTracking()
                    .Where(x => technicianIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.FullName })
                    .ToDictionaryAsync(x => x.Id, x => x.FullName ?? string.Empty);

            var bayIds = diagnoses
                .Where(x => x.AssignedBayId.HasValue && x.AssignedBayId.Value > 0)
                .Select(x => x.AssignedBayId!.Value)
                .Distinct()
                .ToList();
            var bayCodeMap = bayIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.Bays.AsNoTracking()
                    .Where(x => bayIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Code, x.Name })
                    .ToDictionaryAsync(x => x.Id, x => string.IsNullOrWhiteSpace(x.Code) ? (x.Name ?? string.Empty) : x.Code);

            var orderMap = orderRows.ToDictionary(x => x.Id, x => x);
            var now = DateTime.UtcNow;

            var board = diagnoses
                .GroupBy(x => x.ServiceOrderId)
                .Select(group =>
                {
                    var order = orderMap[group.Key];
                    var list = group.ToList();
                    var latest = list
                        .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                        .First();

                    var assignedDiagnosis = list
                        .Where(x => x.Status == DiagnosisStatusAssigned)
                        .OrderByDescending(x => x.AssignedAt ?? x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                        .FirstOrDefault();

                    var pendingSince = list
                        .Where(x => x.Status == DiagnosisStatusPending)
                        .Select(x => x.CreatedAt ?? x.UpdatedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderBy(x => x)
                        .FirstOrDefault();

                    var assignedSince = list
                        .Where(x => x.Status == DiagnosisStatusAssigned)
                        .Select(x => x.AssignedAt ?? x.UpdatedAt ?? x.CreatedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderBy(x => x)
                        .FirstOrDefault();

                    var pendingMinutes = pendingSince == default ? 0 : (int)Math.Floor((now - pendingSince).TotalMinutes);
                    var assignedMinutes = assignedSince == default ? 0 : (int)Math.Floor((now - assignedSince).TotalMinutes);

                    var allCompleted = list.Count > 0 && list.All(x => x.Status == DiagnosisStatusCompleted);
                    var hasAssigned = list.Any(x => x.Status == DiagnosisStatusAssigned);
                    var hasSupport = list.Any(x => x.SupportRequestedAt.HasValue);
                    var hasCancelPending = list.Any(x => string.Equals(x.CancellationStatus, CancellationStatusPending, StringComparison.OrdinalIgnoreCase));
                    var isPendingOverdue = pendingMinutes >= UnclaimedEscalationMinutes;
                    var isAssignedOverdue = assignedMinutes >= DiagnosisSlaMinutes;

                    string queueColor;
                    string queueState;
                    if (hasSupport || hasCancelPending || isPendingOverdue || isAssignedOverdue)
                    {
                        queueColor = "RED";
                        queueState = "ALERT";
                    }
                    else if (allCompleted)
                    {
                        queueColor = "GREEN";
                        queueState = "DIAGNOSED";
                    }
                    else if (hasAssigned)
                    {
                        queueColor = "YELLOW";
                        queueState = "IN_PROGRESS";
                    }
                    else
                    {
                        queueColor = "WHITE";
                        queueState = "NEW";
                    }

                    var teamIdValue = assignedDiagnosis?.AssignedTeamId ?? latest.AssignedTeamId;
                    var technicianId = assignedDiagnosis?.TechnicianId ?? latest.TechnicianId;
                    var assistantTechnicianId = assignedDiagnosis?.AssistantTechnicianId ?? latest.AssistantTechnicianId;
                    var assignedBayId = assignedDiagnosis?.AssignedBayId ?? latest.AssignedBayId;
                    var startedAt = list
                        .Select(x => x.StartedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderBy(x => x)
                        .FirstOrDefault();

                    var technicalApprovedEntries = list
                        .Where(x => x.ApprovedByForemanId.HasValue)
                        .ToList();
                    var technicalApprovedAt = technicalApprovedEntries
                        .Select(x => x.UpdatedAt ?? x.DiagnosedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderByDescending(x => x)
                        .FirstOrDefault();
                    var technicalApprovedBy = technicalApprovedEntries
                        .Select(x => x.ApprovedByForemanId)
                        .FirstOrDefault(x => x.HasValue);

                    var finalApprovedAt = list
                        .Select(x => x.FinalApprovedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderByDescending(x => x)
                        .FirstOrDefault();
                    var finalApprovedBy = list
                        .Select(x => x.FinalApprovedByManagerId)
                        .FirstOrDefault(x => x.HasValue);
                    var cancelReason = list
                        .Select(x => x.CancellationReason)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                    return new DiagnosisDispatchBoardItemDto
                    {
                        ServiceOrderId = group.Key,
                        ServiceOrderCode = order.Code,
                        ExecuteBranchId = order.ExecuteBranchId,
                        ServiceOrderStatus = order.Status,
                        DiagnosisIds = list.Select(x => x.Id).OrderBy(x => x).ToArray(),
                        QueueState = queueState,
                        QueueColor = queueColor,
                        DispatchTag = latest.DispatchTag,
                        TeamId = teamIdValue,
                        TeamName = teamIdValue.HasValue && teamNameMap.TryGetValue(teamIdValue.Value, out var teamName) ? teamName : null,
                        TechnicianId = technicianId > 0 ? technicianId : null,
                        TechnicianName = technicianNameMap.TryGetValue(technicianId, out var technicianName) ? technicianName : null,
                        AssistantTechnicianId = assistantTechnicianId,
                        AssistantTechnicianName = assistantTechnicianId.HasValue && technicianNameMap.TryGetValue(assistantTechnicianId.Value, out var assistantName) ? assistantName : null,
                        AssignedBayId = assignedBayId,
                        AssignedBayCode = assignedBayId.HasValue && bayCodeMap.TryGetValue(assignedBayId.Value, out var bayCode) ? bayCode : null,
                        RequestedAt = pendingSince == default ? latest.CreatedAt : pendingSince,
                        AssignedAt = assignedSince == default ? latest.AssignedAt : assignedSince,
                        StartedAt = startedAt == default ? null : startedAt,
                        PendingMinutes = Math.Max(0, pendingMinutes),
                        AssignedMinutes = Math.Max(0, assignedMinutes),
                        IsSupportRequested = hasSupport,
                        IsCancelPendingApproval = hasCancelPending,
                        CancelReason = cancelReason,
                        TechnicalApprovedByTeamLeaderId = technicalApprovedBy,
                        TechnicalApprovedAt = technicalApprovedAt == default ? null : technicalApprovedAt,
                        FinalApprovedByManagerId = finalApprovedBy,
                        FinalApprovedAt = finalApprovedAt == default ? null : finalApprovedAt,
                    };
                })
                .OrderByDescending(x => x.QueueColor == "RED")
                .ThenByDescending(x => x.QueueColor == "YELLOW")
                .ThenByDescending(x => x.PendingMinutes)
                .ThenByDescending(x => x.AssignedMinutes)
                .ToList();

            return board;
        }

        public async Task RemoveRecommendationsByDiagnosisIdsAsync(IEnumerable<long> diagnosisIds)
        {
            var idList = diagnosisIds?.Distinct().ToList() ?? new List<long>();
            if (idList.Count == 0) return;

            var recs = await _context.DiagnoseRecommendations
                .Where(x => idList.Contains(x.DiagnoseId))
                .ToListAsync();
            if (recs.Count == 0) return;

            _context.DiagnoseRecommendations.RemoveRange(recs);
        }

        public Task RemoveDiagnosesAsync(IEnumerable<Diagnosis> diagnoses)
        {
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count > 0)
            {
                _context.Diagnoses.RemoveRange(list);
            }
            return Task.CompletedTask;
        }
    }
}
