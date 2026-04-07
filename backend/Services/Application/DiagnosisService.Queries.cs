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
        private const string LogTypeCheckIn = "CHECK_IN";

        public async Task<IReadOnlyList<Diagnosis>> GetAllFilteredAsync(long? serviceOrderId, bool requestOnly, long? teamId = null)
        {
            var query = _context.Diagnoses.AsNoTracking().AsQueryable();

            if (serviceOrderId.HasValue)
            {
                query = query.Where(x => x.ServiceOrderId == serviceOrderId.Value);
            }

            if (requestOnly)
            {
                query = query.Where(x => IsPendingLikeStatus(x.Status) || x.Status == DiagnosisStatusAssigned);
            }

            var rows = await query
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();

            if (!teamId.HasValue)
            {
                return rows;
            }

            var normalizedTeamTag = NormalizeDispatchTag(await _context.Teams
                .Where(x => x.Id == teamId.Value)
                .Select(x => x.SpecialtyTag)
                .FirstOrDefaultAsync());

            var filtered = rows.Where(x =>
                x.AssignedTeamId == teamId.Value ||
                (TryParseDispatchCollaborationNote(x.FinalApprovalNote)?.TeamIds.Contains(teamId.Value) ?? false) ||
                (
                    x.AssignedTeamId == null &&
                    IsPendingLikeStatus(x.Status) &&
                    (
                        normalizedTeamTag == null ||
                        string.IsNullOrWhiteSpace(x.DispatchTag) ||
                        SplitDispatchTags(x.DispatchTag).Contains(normalizedTeamTag, StringComparer.OrdinalIgnoreCase)
                    )
                ))
                .ToList();

            return filtered;
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
                    x.LogType == LogTypeCheckIn &&
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

            var diagnoses = await query.ToListAsync();

            if (teamId.HasValue)
            {
                var normalizedTeamTag = NormalizeDispatchTag(await _context.Teams
                    .Where(x => x.Id == teamId.Value)
                    .Select(x => x.SpecialtyTag)
                    .FirstOrDefaultAsync());

                diagnoses = diagnoses
                    .Where(x =>
                        x.AssignedTeamId == teamId.Value ||
                        (TryParseDispatchCollaborationNote(x.FinalApprovalNote)?.TeamIds.Contains(teamId.Value) ?? false) ||
                        (
                            x.AssignedTeamId == null &&
                            IsPendingLikeStatus(x.Status) &&
                            (
                                normalizedTeamTag == null ||
                                string.IsNullOrWhiteSpace(x.DispatchTag) ||
                                SplitDispatchTags(x.DispatchTag).Contains(normalizedTeamTag, StringComparer.OrdinalIgnoreCase)
                            )
                        ))
                    .ToList();
            }

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

            var collaborationTeamIds = diagnoses
                .Select(x => TryParseDispatchCollaborationNote(x.FinalApprovalNote))
                .Where(x => x != null)
                .SelectMany(x => x!.TeamIds)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (collaborationTeamIds.Count > 0)
            {
                teamIds = teamIds
                    .Concat(collaborationTeamIds)
                    .Distinct()
                    .ToList();
            }
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
                        .Where(x => IsPendingLikeStatus(x.Status))
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
                    var hasCancelPending = list.Any(IsCancellationPending);
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

                    var collaborationMeta = list
                        .Select(x => TryParseDispatchCollaborationNote(x.FinalApprovalNote))
                        .FirstOrDefault(x => x != null);

                    // Only expose assignment identity when there is an actual handled diagnosis
                    // (ASSIGNED/COMPLETED). Pending rows can carry unrelated ids from request context.
                    // In multi-team mode, prefer the diagnosis row of the primary team for stable display.
                    var preferredPrimaryTeamId = collaborationMeta?.PrimaryTeamId;
                    var handledDiagnosis = list
                        .Where(x => x.Status == DiagnosisStatusAssigned || x.Status == DiagnosisStatusCompleted)
                        .OrderByDescending(x => preferredPrimaryTeamId.HasValue && x.AssignedTeamId == preferredPrimaryTeamId.Value)
                        .ThenByDescending(x => x.AssignedAt ?? x.DiagnosedAt ?? x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                        .FirstOrDefault();

                    var assignedTeamIds = list
                        .Where(x => x.AssignedTeamId.HasValue && x.AssignedTeamId.Value > 0)
                        .Select(x => x.AssignedTeamId!.Value)
                        .Distinct()
                        .ToList();

                    if (collaborationMeta?.TeamIds?.Count > 0)
                    {
                        assignedTeamIds = collaborationMeta.TeamIds
                            .Concat(assignedTeamIds)
                            .Distinct()
                            .ToList();
                    }

                    var primaryTeamId = collaborationMeta?.PrimaryTeamId ?? handledDiagnosis?.AssignedTeamId;
                    if (!primaryTeamId.HasValue && assignedTeamIds.Count > 0)
                    {
                        primaryTeamId = assignedTeamIds[0];
                    }

                    if (primaryTeamId.HasValue && !assignedTeamIds.Contains(primaryTeamId.Value))
                    {
                        assignedTeamIds.Insert(0, primaryTeamId.Value);
                    }

                    var assignedTeamNames = assignedTeamIds
                        .Select(id => teamNameMap.TryGetValue(id, out var teamName) ? teamName : string.Empty)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var primaryTeamName = primaryTeamId.HasValue && teamNameMap.TryGetValue(primaryTeamId.Value, out var resolvedPrimaryTeamName)
                        ? resolvedPrimaryTeamName
                        : null;

                    var technicianId = handledDiagnosis?.TechnicianId ?? 0;
                    var assistantTechnicianId = handledDiagnosis?.AssistantTechnicianId;
                    var assignedBayId = handledDiagnosis?.AssignedBayId;
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
                        TeamId = primaryTeamId,
                        TeamName = primaryTeamName,
                        PrimaryTeamId = primaryTeamId,
                        PrimaryTeamName = primaryTeamName,
                        AssignedTeamIds = assignedTeamIds,
                        AssignedTeamNames = assignedTeamNames,
                        TechnicianId = technicianId > 0 ? technicianId : null,
                        TechnicianName = technicianNameMap.TryGetValue(technicianId, out var technicianName) ? technicianName : null,
                        AssistantTechnicianId = assistantTechnicianId,
                        AssistantTechnicianName = assistantTechnicianId.HasValue && technicianNameMap.TryGetValue(assistantTechnicianId.Value, out var assistantName) ? assistantName : null,
                        AssignedBayId = assignedBayId,
                        AssignedBayCode = assignedBayId.HasValue && bayCodeMap.TryGetValue(assignedBayId.Value, out var bayCode) ? bayCode : null,
                        RequestedAt = pendingSince == default ? latest.CreatedAt : pendingSince,
                        AssignedAt = assignedSince == default ? null : assignedSince,
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
