using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class DiagnosisService
    {
        private const string LogTypeCheckIn = "CHECK_IN";

        private static string ResolveDiagnosisWorkflowStatus(
            Diagnosis latestDiagnosis,
            bool hasAssigned,
            bool isEscalated,
            string? teamLeaderApprovalStatus,
            string? finalApprovalStatus)
        {
            static bool EqualsCode(string? source, string code) =>
                string.Equals(source?.Trim(), code, StringComparison.OrdinalIgnoreCase);

            var diagnosisStatus = (latestDiagnosis.Status ?? string.Empty).Trim();
            if (EqualsCode(diagnosisStatus, nameof(DiagnosisStatus.CANCELED)))
            {
                return nameof(DiagnosisWorkflowStatusCode.CANCELED);
            }

            if (EqualsCode(finalApprovalStatus, nameof(ApprovalDecisionStatusCode.APPROVED)))
            {
                return nameof(DiagnosisWorkflowStatusCode.FOREMAN_APPROVED_LOCKED);
            }

            if (EqualsCode(finalApprovalStatus, nameof(ApprovalDecisionStatusCode.REJECTED)))
            {
                return nameof(DiagnosisWorkflowStatusCode.FOREMAN_REJECTED_REDIAGNOSE);
            }

            if (EqualsCode(teamLeaderApprovalStatus, nameof(ApprovalDecisionStatusCode.REJECTED)))
            {
                return nameof(DiagnosisWorkflowStatusCode.TL_REJECTED_REDIAGNOSE);
            }

            if (EqualsCode(teamLeaderApprovalStatus, nameof(ApprovalDecisionStatusCode.BYPASSED)) ||
                EqualsCode(teamLeaderApprovalStatus, nameof(ApprovalDecisionStatusCode.ESCALATED)) ||
                isEscalated)
            {
                return nameof(DiagnosisWorkflowStatusCode.ESCALATED_TO_FOREMAN);
            }

            if (EqualsCode(teamLeaderApprovalStatus, nameof(ApprovalDecisionStatusCode.APPROVED)))
            {
                return nameof(DiagnosisWorkflowStatusCode.TL_APPROVED);
            }

            if (latestDiagnosis.SubmittedAt.HasValue)
            {
                return nameof(DiagnosisWorkflowStatusCode.SUBMITTED);
            }

            return hasAssigned
                ? nameof(DiagnosisWorkflowStatusCode.IN_PROGRESS)
                : nameof(DiagnosisWorkflowStatusCode.NEW);
        }

        public async Task<IReadOnlyList<Diagnosis>> GetAllFilteredAsync(long? serviceOrderId, bool requestOnly, long? teamId = null)
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

            var rows = await query
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ToListAsync();
            await ApplyLatestLogOverlayAsync(rows);

            if (!teamId.HasValue)
            {
                return rows;
            }

            var teamProfile = await _context.Teams
                .Where(x => x.Id == teamId.Value)
                .Select(x => new { x.SpecialtyTag, x.Code })
                .FirstOrDefaultAsync();

            var filtered = rows.Where(x =>
                GetAssignedTeamId(x) == teamId.Value ||
                (TryParseDispatchCollaborationNote(null, GetForemanNote(x))?.TeamIds.Contains(teamId.Value) ?? false) ||
                (
                    !GetAssignedTeamId(x).HasValue &&
                    IsPendingLikeStatus(x.Status) &&
                    (
                        teamProfile == null ||
                        string.IsNullOrWhiteSpace(x.DispatchTag) ||
                        SplitDispatchTags(x.DispatchTag).Any(tag => DispatchTagMatchesTeam(tag, teamProfile.SpecialtyTag, teamProfile.Code))
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
            var rows = await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .ToListAsync();
            await ApplyLatestLogOverlayAsync(rows);
            return rows;
        }

        public async Task<IReadOnlyList<Diagnosis>> GetLatestWorkflowDiagnosesByServiceOrderAsync(long serviceOrderId)
        {
            var rows = await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .ToListAsync();
            await ApplyLatestLogOverlayAsync(rows);
            if (rows.Count == 0)
            {
                return rows;
            }

            static bool IsFinalDecided(string? finalStatus) =>
                string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.REJECTED), StringComparison.OrdinalIgnoreCase);

            var candidates = rows
                .Where(x => !string.Equals(x.Status, DiagnosisStatusCanceled, StringComparison.OrdinalIgnoreCase))
                .Where(x =>
                {
                    var supportNote = GetSupportRequestNote(x) ?? string.Empty;
                    return !supportNote.Contains("[SUPERSEDED_REQUEUED]", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            var unresolved = candidates
                .Where(x => !IsFinalDecided(GetFinalApprovalStatus(x)))
                .ToList();

            if (unresolved.Count > 0)
            {
                return unresolved;
            }

            return candidates;
        }

        public async Task<IReadOnlyList<DiagnosisDispatchBoardItemDto>> GetDispatchBoardAsync(long? branchId, long? teamId, bool includeCompleted)
        {
            var query = _context.Diagnoses.AsNoTracking().AsQueryable();

            if (!includeCompleted)
            {
                query = query.Where(x => x.Status != DiagnosisStatusCompleted && x.Status != DiagnosisStatusCanceled);
            }

            var diagnoses = await query.ToListAsync();
            await ApplyLatestLogOverlayAsync(diagnoses);

            if (teamId.HasValue)
            {
                var teamProfile = await _context.Teams
                    .Where(x => x.Id == teamId.Value)
                    .Select(x => new { x.SpecialtyTag, x.Code })
                    .FirstOrDefaultAsync();

                diagnoses = diagnoses
                    .Where(x =>
                        GetAssignedTeamId(x) == teamId.Value ||
                        (TryParseDispatchCollaborationNote(null, GetForemanNote(x))?.TeamIds.Contains(teamId.Value) ?? false) ||
                        (
                            !GetAssignedTeamId(x).HasValue &&
                            IsPendingLikeStatus(x.Status) &&
                            (
                                teamProfile == null ||
                                string.IsNullOrWhiteSpace(x.DispatchTag) ||
                                SplitDispatchTags(x.DispatchTag).Any(tag => DispatchTagMatchesTeam(tag, teamProfile.SpecialtyTag, teamProfile.Code))
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
                .Select(GetAssignedTeamId)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var collaborationTeamIds = diagnoses
                .Select(x => TryParseDispatchCollaborationNote(null, GetForemanNote(x)))
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
                    GetTechnicianId(x),
                    GetAssistantTechnicianId(x),
                    GetApprovedByForemanId(x),
                })
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var collaborationTechnicianIds = diagnoses
                .Select(x => TryParseDispatchCollaborationNote(null, GetForemanNote(x)))
                .Where(x => x != null)
                .SelectMany(x => x!.TeamAssignments)
                .SelectMany(x => new long?[] { x.TechnicianId, x.AssistantTechnicianId })
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            if (collaborationTechnicianIds.Count > 0)
            {
                technicianIds = technicianIds
                    .Concat(collaborationTechnicianIds)
                    .Distinct()
                    .ToList();
            }
            var technicianNameMap = technicianIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.Staffs.AsNoTracking()
                    .Where(x => technicianIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.FullName })
                    .ToDictionaryAsync(x => x.Id, x => x.FullName ?? string.Empty);

            var bayIds = diagnoses
                .Select(GetAssignedBayId)
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
            var bayCodeMap = bayIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.Bays.AsNoTracking()
                    .Where(x => bayIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Code, x.Name })
                    .ToDictionaryAsync(x => x.Id, x => string.IsNullOrWhiteSpace(x.Code) ? (x.Name ?? string.Empty) : x.Code);

            var orderMap = orderRows.ToDictionary(x => x.Id, x => x);
            var now = VietnamTime.Now;

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
                        .OrderByDescending(x => GetAssignedAt(x) ?? x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
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
                        .Select(x => GetAssignedAt(x) ?? x.UpdatedAt ?? x.CreatedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderBy(x => x)
                        .FirstOrDefault();

                    var pendingMinutes = pendingSince == default ? 0 : (int)Math.Floor((now - pendingSince).TotalMinutes);
                    var assignedMinutes = assignedSince == default ? 0 : (int)Math.Floor((now - assignedSince).TotalMinutes);

                    var allCompleted = list.Count > 0 && list.All(x => x.Status == DiagnosisStatusCompleted);
                    var hasAssigned = list.Any(x => x.Status == DiagnosisStatusAssigned);
                    var hasSupport = list.Any(x => GetSupportRequestedAt(x).HasValue);
                    var hasCancelPending = list.Any(IsCancellationPending);
                    var isPendingOverdue = pendingMinutes >= UnclaimedEscalationMinutes;
                    var isAssignedOverdue = assignedMinutes >= DiagnosisSlaMinutes;
                    var latestSupportEntry = list
                        .Where(x => GetSupportRequestedAt(x).HasValue)
                        .OrderByDescending(x => GetSupportRequestedAt(x) ?? DateTime.MinValue)
                        .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                        .FirstOrDefault();

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
                        .Select(x => TryParseDispatchCollaborationNote(null, GetForemanNote(x)))
                        .FirstOrDefault(x => x != null);

                    // Only expose assignment identity when there is an actual handled diagnosis
                    // (ASSIGNED/COMPLETED). Pending rows can carry unrelated ids from request context.
                    // In multi-team mode, prefer the diagnosis row of the primary team for stable display.
                    var preferredPrimaryTeamId = collaborationMeta?.PrimaryTeamId;
                    var handledDiagnosis = list
                        .Where(x => x.Status == DiagnosisStatusAssigned || x.Status == DiagnosisStatusCompleted)
                        .OrderByDescending(x => preferredPrimaryTeamId.HasValue && GetAssignedTeamId(x) == preferredPrimaryTeamId.Value)
                        .ThenByDescending(x => GetAssignedAt(x) ?? x.DiagnosedAt ?? x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                        .FirstOrDefault();

                    var assignedTeamIds = list
                        .Select(GetAssignedTeamId)
                        .Where(x => x.HasValue && x.Value > 0)
                        .Select(x => x!.Value)
                        .Distinct()
                        .ToList();

                    if (collaborationMeta?.TeamIds?.Count > 0)
                    {
                        assignedTeamIds = collaborationMeta.TeamIds
                            .Concat(assignedTeamIds)
                            .Distinct()
                            .ToList();
                    }

                    var primaryTeamId = collaborationMeta?.PrimaryTeamId ?? (handledDiagnosis != null ? GetAssignedTeamId(handledDiagnosis) : null);
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

                    string? primaryTeamName = null;
                    if (primaryTeamId.HasValue && teamNameMap.TryGetValue(primaryTeamId.Value, out var resolvedPrimaryTeamName))
                    {
                        primaryTeamName = resolvedPrimaryTeamName;
                    }

                    List<DiagnosisSupportingAssignmentDto> supportingAssignments;
                    if (collaborationMeta?.TeamAssignments?.Count > 0)
                    {
                        supportingAssignments = collaborationMeta.TeamAssignments
                            .Where(x => x.TeamId > 0 && (!primaryTeamId.HasValue || x.TeamId != primaryTeamId.Value))
                            .GroupBy(x => x.TeamId)
                            .Select(group =>
                            {
                                var assignment = group.First();
                                return new DiagnosisSupportingAssignmentDto
                                {
                                    TeamId = group.Key,
                                    TeamName = teamNameMap.TryGetValue(group.Key, out var supportingTeamName) ? supportingTeamName : null,
                                    TechnicianId = assignment.TechnicianId.HasValue && assignment.TechnicianId.Value > 0
                                        ? assignment.TechnicianId.Value
                                        : null,
                                    TechnicianName = assignment.TechnicianId.HasValue && assignment.TechnicianId.Value > 0 && technicianNameMap.TryGetValue(assignment.TechnicianId.Value, out var supportingTechnicianName)
                                        ? supportingTechnicianName
                                        : null,
                                    AssistantTechnicianId = assignment.AssistantTechnicianId.HasValue && assignment.AssistantTechnicianId.Value > 0
                                        ? assignment.AssistantTechnicianId.Value
                                        : null,
                                    AssistantTechnicianName = assignment.AssistantTechnicianId.HasValue && assignment.AssistantTechnicianId.Value > 0 && technicianNameMap.TryGetValue(assignment.AssistantTechnicianId.Value, out var supportingAssistantName)
                                        ? supportingAssistantName
                                        : null,
                                    RequestedTechnicianCount = assignment.RequestedTechnicianCount.HasValue && assignment.RequestedTechnicianCount.Value > 0
                                        ? assignment.RequestedTechnicianCount.Value
                                        : null
                                };
                            })
                            .OrderBy(x => x.TeamName ?? string.Empty)
                            .ToList();
                    }
                    else
                    {
                        supportingAssignments = list
                            .Where(x => GetAssignedTeamId(x).HasValue && GetAssignedTeamId(x)!.Value > 0)
                            .Where(x => !primaryTeamId.HasValue || GetAssignedTeamId(x).GetValueOrDefault() != primaryTeamId.Value)
                            .GroupBy(x => GetAssignedTeamId(x)!.Value)
                            .Select(group =>
                            {
                                var latestTeamDiagnosis = group
                                    .OrderByDescending(x => GetAssignedAt(x) ?? x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                                    .First();

                                return new DiagnosisSupportingAssignmentDto
                                {
                                    TeamId = group.Key,
                                    TeamName = teamNameMap.TryGetValue(group.Key, out var supportingTeamName) ? supportingTeamName : null,
                                    TechnicianId = GetTechnicianId(latestTeamDiagnosis).HasValue && GetTechnicianId(latestTeamDiagnosis)!.Value > 0
                                        ? GetTechnicianId(latestTeamDiagnosis)!.Value
                                        : null,
                                    TechnicianName = GetTechnicianId(latestTeamDiagnosis).HasValue
                                        && GetTechnicianId(latestTeamDiagnosis)!.Value > 0
                                        && technicianNameMap.TryGetValue(GetTechnicianId(latestTeamDiagnosis)!.Value, out var supportingTechnicianName)
                                        ? supportingTechnicianName
                                        : null,
                                    AssistantTechnicianId = GetAssistantTechnicianId(latestTeamDiagnosis),
                                    AssistantTechnicianName = GetAssistantTechnicianId(latestTeamDiagnosis).HasValue && technicianNameMap.TryGetValue(GetAssistantTechnicianId(latestTeamDiagnosis)!.Value, out var supportingAssistantName)
                                        ? supportingAssistantName
                                        : null,
                                    RequestedTechnicianCount = null
                                };
                            })
                            .OrderBy(x => x.TeamName ?? string.Empty)
                            .ToList();
                    }

                    var technicianId = handledDiagnosis != null ? GetTechnicianId(handledDiagnosis) ?? 0 : 0;
                    var assistantTechnicianId = handledDiagnosis != null ? GetAssistantTechnicianId(handledDiagnosis) : null;
                    var assignedBayId = handledDiagnosis != null ? GetAssignedBayId(handledDiagnosis) : null;
                    var technicianAcceptedAt = list
                        .Select(GetTechnicianAcceptedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderBy(x => x)
                        .FirstOrDefault();
                    var technicianCompletedAt = list
                        .Select(GetTechnicianCompletedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderByDescending(x => x)
                        .FirstOrDefault();

                    var technicalApprovedEntries = list
                        .Where(x => GetApprovedByForemanId(x).HasValue)
                        .ToList();
                    var technicalApprovedAt = technicalApprovedEntries
                        .Select(x => x.UpdatedAt ?? x.DiagnosedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderByDescending(x => x)
                        .FirstOrDefault();
                    var technicalApprovedBy = technicalApprovedEntries
                        .Select(x => GetApprovedByForemanId(x))
                        .FirstOrDefault(x => x.HasValue);
                    var approvedByForemanAt = list
                        .Select(x => GetApprovedByForemanAt(x))
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderByDescending(x => x)
                        .FirstOrDefault();

                    var approvedByTeamLeaderAt = list
                        .Select(x => GetApprovedByTeamLeaderAt(x))
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderByDescending(x => x)
                        .FirstOrDefault();
                    var teamLeaderApprovalStatus = list
                        .Select(x => GetTeamLeaderApprovalStatus(x))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    var finalApprovalStatus = list
                        .Select(x => GetFinalApprovalStatus(x))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    var isEscalated = list.Any(x => x.IsEscalated);
                    var teamLeaderNote = list
                        .Select(x => GetTeamLeaderNote(x))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    var foremanNote = list
                        .Select(x => GetForemanNote(x))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    var cancelReason = list
                        .Select(GetCancellationReason)
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    var workflowStatus = ResolveDiagnosisWorkflowStatus(
                        latest,
                        hasAssigned,
                        isEscalated,
                        teamLeaderApprovalStatus,
                        finalApprovalStatus);

                    return new DiagnosisDispatchBoardItemDto
                    {
                        ServiceOrderId = group.Key,
                        ServiceOrderCode = order.Code,
                        ExecuteBranchId = order.ExecuteBranchId,
                        ServiceOrderStatus = order.Status,
                        DiagnosisIds = list.Select(x => x.Id).OrderBy(x => x).ToArray(),
                        QueueState = queueState,
                        QueueColor = queueColor,
                        WorkflowStatus = workflowStatus,
                        DispatchTag = latest.DispatchTag,
                        TeamId = primaryTeamId,
                        TeamName = primaryTeamName,
                        PrimaryTeamId = primaryTeamId,
                        PrimaryTeamName = primaryTeamName,
                        AssignedTeamIds = assignedTeamIds,
                        AssignedTeamNames = assignedTeamNames,
                        SupportingAssignments = supportingAssignments,
                        TechnicianId = technicianId > 0 ? technicianId : null,
                        TechnicianName = technicianNameMap.TryGetValue(technicianId, out var technicianName) ? technicianName : null,
                        AssistantTechnicianId = assistantTechnicianId,
                        AssistantTechnicianName = assistantTechnicianId.HasValue && technicianNameMap.TryGetValue(assistantTechnicianId.Value, out var assistantName) ? assistantName : null,
                        AssignedBayId = assignedBayId,
                        AssignedBayCode = assignedBayId.HasValue && bayCodeMap.TryGetValue(assignedBayId.Value, out var bayCode) ? bayCode : null,
                        RequestedAt = pendingSince == default ? latest.CreatedAt : pendingSince,
                        AssignedAt = assignedSince == default ? null : assignedSince,
                        StartedAt = technicianAcceptedAt == default ? null : technicianAcceptedAt,
                        TechnicianAcceptedAt = technicianAcceptedAt == default ? null : technicianAcceptedAt,
                        TechnicianCompletedAt = technicianCompletedAt == default ? null : technicianCompletedAt,
                        PendingMinutes = Math.Max(0, pendingMinutes),
                        AssignedMinutes = Math.Max(0, assignedMinutes),
                        IsSupportRequested = hasSupport,
                        SupportRequestedAt = latestSupportEntry != null ? GetSupportRequestedAt(latestSupportEntry) : null,
                        SupportRequestedBy = latestSupportEntry != null ? GetSupportRequestedBy(latestSupportEntry) : null,
                        SupportRequestNote = latestSupportEntry != null ? GetSupportRequestNote(latestSupportEntry) : null,
                        IsCancelPendingApproval = hasCancelPending,
                        CancelReason = cancelReason,
                        TechnicalApprovedByTeamLeaderId = technicalApprovedBy,
                        ApprovedByForemanAt = approvedByForemanAt == default ? null : approvedByForemanAt,
                        TechnicalApprovedAt = technicalApprovedAt == default ? null : technicalApprovedAt,
                        ApprovedByTeamLeaderAt = approvedByTeamLeaderAt == default ? null : approvedByTeamLeaderAt,
                        TeamLeaderNote = teamLeaderNote,
                        ForemanNote = foremanNote,
                        TeamLeaderApprovalStatus = teamLeaderApprovalStatus,
                        ForemanApprovalStatus = finalApprovalStatus,
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

            var assignmentIds = await _context.DiagnosisAssignments.AsNoTracking()
                .Where(x => idList.Contains(x.DiagnosisId))
                .Select(x => x.Id)
                .Distinct()
                .ToListAsync();

            var recs = await _context.DiagnoseRecommendations
                .Where(x => idList.Contains(x.DiagnosisId) ||
                            (x.DiagnosisAssignmentId.HasValue && assignmentIds.Contains(x.DiagnosisAssignmentId.Value)))
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

        public async Task ArchiveDiagnosesAsSupersededAsync(IEnumerable<Diagnosis> diagnoses, long? actorStaffId = null, string? auditTag = null)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var tag = string.IsNullOrWhiteSpace(auditTag) ? "[SUPERSEDED_REQUEUED]" : auditTag.Trim();
            foreach (var diagnosis in list)
            {
                diagnosis.Status = DiagnosisStatusCompleted;
                diagnosis.DiagnosedAt ??= now;
                SetCancellationStatus(diagnosis, nameof(DiagnosisCancellationStatusCode.NONE));
                SetCancellationRequestedAt(diagnosis, null);
                SetCancellationRequestedBy(diagnosis, null);
                SetCancellationDecisionAt(diagnosis, null);
                SetCancellationDecisionBy(diagnosis, null);
                SetCancellationDecisionNote(diagnosis, null);
                var supportNote = GetSupportRequestNote(diagnosis);
                SetSupportRequestNote(diagnosis, string.IsNullOrWhiteSpace(supportNote)
                    ? tag
                    : supportNote!.Contains(tag, StringComparison.OrdinalIgnoreCase)
                        ? supportNote
                        : $"{supportNote.Trim()} {tag}");
                diagnosis.UpdatedAt = now;
                diagnosis.UpdatedBy = actorStaffId;
            }

            _context.Diagnoses.UpdateRange(list);
            AppendDiagnosisAssignments(list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    diagnosis,
                    DiagnoseLogEventArchived,
                    oldStatus: DiagnosisStatusAssigned,
                    createdBy: actorStaffId,
                    note: tag)));
            await _context.SaveChangesAsync();
        }
    }
}





