using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisAssignmentStateHelpers;
using static MyGarage.Services.DiagnosisDispatchCollaborationHelpers;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    public class DiagnosisDispatchBoardService : IDiagnosisDispatchBoardService
    {
        private readonly IDiagnosisStore _store;

        public DiagnosisDispatchBoardService(IDiagnosisStore store)
        {
            _store = store;
        }

        public async Task<IReadOnlyList<DiagnosisDispatchBoardItemDto>> GetDispatchBoardAsync(long? branchId, long? teamId, bool includeCompleted, string? viewMode = null)
        {
            var diagnoses = (await _store.GetDispatchBoardDiagnosesNoTrackingAsync(includeCompleted)).ToList();
            await ApplyLatestLogOverlayAsync(_store, diagnoses);

            if (teamId.HasValue)
            {
                var teamProfile = await _store.GetTeamProfileAsync(teamId.Value);

                diagnoses = diagnoses
                    .Where(x =>
                        GetAssignedTeamId(_store, x) == teamId.Value ||
                        (TryParseDispatchCollaborationNote(null, GetForemanNote(_store, x))?.TeamIds.Contains(teamId.Value) ?? false) ||
                        (
                            !GetAssignedTeamId(_store, x).HasValue &&
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
            var orderRows = await _store.GetServiceOrderBoardRowsAsync(serviceOrderIds, branchId);

            if (orderRows.Count == 0)
            {
                return Array.Empty<DiagnosisDispatchBoardItemDto>();
            }

            var orderIds = orderRows.Select(x => x.Id).ToHashSet();
            diagnoses = diagnoses.Where(x => orderIds.Contains(x.ServiceOrderId)).ToList();

            var teamIds = diagnoses
                .Select(x => GetAssignedTeamId(_store, x))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var collaborationTeamIds = diagnoses
                .Select(x => TryParseDispatchCollaborationNote(null, GetForemanNote(_store, x)))
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
                : await _store.GetTeamNameMapAsync(teamIds);

            var technicianIds = diagnoses
                .SelectMany(x => new long?[]
                {
                    GetTechnicianId(_store, x),
                    GetAssistantTechnicianId(_store, x),
                    GetApprovedByForemanId(_store, x),
                })
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var collaborationTechnicianIds = diagnoses
                .Select(x => TryParseDispatchCollaborationNote(null, GetForemanNote(_store, x)))
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
                : await _store.GetStaffNameMapAsync(technicianIds);

            var bayIds = diagnoses
                .Select(x => GetAssignedBayId(_store, x))
                .Where(x => x.HasValue && x.Value > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
            var bayCodeMap = bayIds.Count == 0
                ? new Dictionary<long, string>()
                : await _store.GetBayCodeMapAsync(bayIds);

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

                    bool HasAssignmentContext(Diagnosis x) =>
                        x.Status == DiagnosisStatusAssigned ||
                        GetAssignedTeamId(_store, x).HasValue ||
                        GetTechnicianId(_store, x).HasValue ||
                        GetAssistantTechnicianId(_store, x).HasValue ||
                        GetAssignedAt(_store, x).HasValue ||
                        GetTechnicianAcceptedAt(_store, x).HasValue;

                    var pendingSince = list
                        .Where(x => IsPendingLikeStatus(x.Status) && !HasAssignmentContext(x))
                        .Select(x => x.CreatedAt ?? x.UpdatedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderBy(x => x)
                        .FirstOrDefault();

                    var assignedSince = list
                        .Where(HasAssignmentContext)
                        .Select(x => GetAssignedAt(_store, x) ?? x.UpdatedAt ?? x.CreatedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderBy(x => x)
                        .FirstOrDefault();

                    var pendingReference = pendingSince == default
                        ? (latest.CreatedAt ?? latest.UpdatedAt ?? DateTime.MinValue)
                        : pendingSince;
                    var pendingMinutes = pendingReference == default ? 0 : (int)Math.Floor((now - pendingReference).TotalMinutes);
                    var assignedMinutes = assignedSince == default ? 0 : (int)Math.Floor((now - assignedSince).TotalMinutes);

                    var allCompleted = list.Count > 0 && list.All(x => x.Status == DiagnosisStatusCompleted);
                    var hasAssigned = list.Any(HasAssignmentContext);
                    var hasSupport = list.Any(x => GetSupportRequestedAt(_store, x).HasValue);
                    var hasCancelPending = list.Any(x => IsCancellationPending(_store, x));
                    var isPendingOverdue = pendingMinutes >= UnclaimedEscalationMinutes;
                    var isAssignedOverdue = assignedMinutes >= DiagnosisSlaMinutes;
                    var latestSupportEntry = list
                        .Where(x => GetSupportRequestedAt(_store, x).HasValue)
                        .OrderByDescending(x => GetSupportRequestedAt(_store, x) ?? DateTime.MinValue)
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
                        .Select(x => TryParseDispatchCollaborationNote(null, GetForemanNote(_store, x)))
                        .FirstOrDefault(x => x != null);

                    // Only expose assignment identity when there is an actual handled diagnosis
                    // (ASSIGNED/COMPLETED). Pending rows can carry unrelated ids from request context.
                    // In multi-team mode, prefer the diagnosis row of the primary team for stable display.
                    var preferredPrimaryTeamId = collaborationMeta?.PrimaryTeamId;
                    var handledDiagnosis = list
                        .Where(x => HasAssignmentContext(x) || x.Status == DiagnosisStatusCompleted)
                        .OrderByDescending(x => preferredPrimaryTeamId.HasValue && GetAssignedTeamId(_store, x) == preferredPrimaryTeamId.Value)
                        .ThenByDescending(x => GetAssignedAt(_store, x) ?? x.DiagnosedAt ?? x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                        .FirstOrDefault();

                    var assignedTeamIds = list
                        .Select(x => GetAssignedTeamId(_store, x))
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

                    var primaryTeamId = collaborationMeta?.PrimaryTeamId ?? (handledDiagnosis != null ? GetAssignedTeamId(_store, handledDiagnosis) : null);
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
                            .Where(x => GetAssignedTeamId(_store, x).HasValue && GetAssignedTeamId(_store, x)!.Value > 0)
                            .Where(x => !primaryTeamId.HasValue || GetAssignedTeamId(_store, x).GetValueOrDefault() != primaryTeamId.Value)
                            .GroupBy(x => GetAssignedTeamId(_store, x)!.Value)
                            .Select(group =>
                            {
                                var latestTeamDiagnosis = group
                                    .OrderByDescending(x => GetAssignedAt(_store, x) ?? x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                                    .First();

                                var latestTechId = GetTechnicianId(_store, latestTeamDiagnosis);
                                var latestAssistantId = GetAssistantTechnicianId(_store, latestTeamDiagnosis);

                                return new DiagnosisSupportingAssignmentDto
                                {
                                    TeamId = group.Key,
                                    TeamName = teamNameMap.TryGetValue(group.Key, out var supportingTeamName) ? supportingTeamName : null,
                                    TechnicianId = latestTechId.HasValue && latestTechId.Value > 0
                                        ? latestTechId.Value
                                        : null,
                                    TechnicianName = latestTechId.HasValue
                                        && latestTechId.Value > 0
                                        && technicianNameMap.TryGetValue(latestTechId.Value, out var supportingTechnicianName)
                                        ? supportingTechnicianName
                                        : null,
                                    AssistantTechnicianId = latestAssistantId,
                                    AssistantTechnicianName = latestAssistantId.HasValue && technicianNameMap.TryGetValue(latestAssistantId.Value, out var supportingAssistantName)
                                        ? supportingAssistantName
                                        : null,
                                    RequestedTechnicianCount = null
                                };
                            })
                            .OrderBy(x => x.TeamName ?? string.Empty)
                            .ToList();
                    }

                    var technicianId = handledDiagnosis != null ? GetTechnicianId(_store, handledDiagnosis) ?? 0 : 0;
                    var assistantTechnicianId = handledDiagnosis != null ? GetAssistantTechnicianId(_store, handledDiagnosis) : null;
                    var baySourceDiagnosis = handledDiagnosis;
                    if (baySourceDiagnosis == null || GetAssignedBayId(_store, baySourceDiagnosis) is null)
                    {
                        baySourceDiagnosis = list
                            .Where(x => GetAssignedBayId(_store, x).HasValue && GetAssignedBayId(_store, x)!.Value > 0)
                            .OrderByDescending(x => GetAssignedAt(_store, x) ?? x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                            .FirstOrDefault();
                    }
                    var assignedBayId = baySourceDiagnosis != null ? GetAssignedBayId(_store, baySourceDiagnosis) : null;
                    var technicianAcceptedAt = list
                        .Select(x => GetTechnicianAcceptedAt(_store, x))
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderBy(x => x)
                        .FirstOrDefault();
                    var technicianCompletedAt = list
                        .Select(x => GetTechnicianCompletedAt(_store, x))
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderByDescending(x => x)
                        .FirstOrDefault();

                    var technicalApprovedEntries = list
                        .Where(x => GetApprovedByForemanId(_store, x).HasValue)
                        .ToList();
                    var technicalApprovedAt = technicalApprovedEntries
                        .Select(x => x.UpdatedAt ?? x.DiagnosedAt)
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderByDescending(x => x)
                        .FirstOrDefault();
                    var technicalApprovedBy = technicalApprovedEntries
                        .Select(x => GetApprovedByForemanId(_store, x))
                        .FirstOrDefault(x => x.HasValue);
                    var approvedByForemanAt = list
                        .Select(x => GetApprovedByForemanAt(_store, x))
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderByDescending(x => x)
                        .FirstOrDefault();

                    var approvedByTeamLeaderAt = list
                        .Select(x => GetApprovedByTeamLeaderAt(_store, x))
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value)
                        .OrderByDescending(x => x)
                        .FirstOrDefault();
                    var teamLeaderApprovalStatus = list
                        .Select(x => GetTeamLeaderApprovalStatus(_store, x))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    var finalApprovalStatus = list
                        .Select(x => GetFinalApprovalStatus(_store, x))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    var isEscalated = list.Any(x => x.IsEscalated);
                    static bool IsDecisionPending(string? value) =>
                        string.IsNullOrWhiteSpace(value) ||
                        string.Equals(value, nameof(ApprovalDecisionStatusCode.PENDING), StringComparison.OrdinalIgnoreCase);

                    static bool IsDecisionFinalized(string? value) =>
                        string.Equals(value, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, nameof(ApprovalDecisionStatusCode.REJECTED), StringComparison.OrdinalIgnoreCase);

                    var hasPendingTeamLeaderApproval = list.Any(x =>
                    {
                        var assignmentStatus = GetAssignmentStatus(_store, x);
                        var assignmentEventType = GetAssignmentEventType(_store, x);
                        var submittedByAssignment =
                            string.Equals(assignmentStatus, DiagnosisStatusCompleted, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(assignmentEventType, DiagnoseLogEventCompleted, StringComparison.OrdinalIgnoreCase);

                        var submittedByDiagnosis = x.SubmittedAt.HasValue ||
                            string.Equals(x.Status, DiagnosisStatusCompleted, StringComparison.OrdinalIgnoreCase);

                        if (!submittedByAssignment && !submittedByDiagnosis)
                        {
                            return false;
                        }

                        var tlStatus = GetTeamLeaderApprovalStatus(_store, x);
                        var foremanStatus = GetFinalApprovalStatus(_store, x);
                        return IsDecisionPending(tlStatus) && !IsDecisionFinalized(foremanStatus);
                    });
                    var teamLeaderNote = list
                        .Select(x => GetTeamLeaderNote(_store, x))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    var foremanNote = list
                        .Select(x => GetForemanNote(_store, x))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    var cancelReason = list
                        .Select(x => GetCancellationReason(_store, x))
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                    var workflowStatus = ResolveDiagnosisWorkflowStatus(
                        latest,
                        hasAssigned,
                        isEscalated,
                        hasPendingTeamLeaderApproval,
                        teamLeaderApprovalStatus,
                        finalApprovalStatus);
                    var dispatchBucket = ResolveDispatchBucket(
                        latest.Status,
                        workflowStatus,
                        viewMode,
                        primaryTeamId.HasValue && primaryTeamId.Value > 0,
                        technicianId > 0,
                        Math.Max(0, pendingMinutes));
                    if (string.IsNullOrWhiteSpace(dispatchBucket))
                    {
                        return null;
                    }

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
                        DispatchBucket = dispatchBucket,
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
                        SupportRequestedAt = latestSupportEntry != null ? GetSupportRequestedAt(_store, latestSupportEntry) : null,
                        SupportRequestedBy = latestSupportEntry != null ? GetSupportRequestedBy(_store, latestSupportEntry) : null,
                        SupportRequestNote = latestSupportEntry != null ? GetSupportRequestNote(_store, latestSupportEntry) : null,
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
                .Where(x => x != null)
                .Select(x => x!)
                .OrderByDescending(x => x.QueueColor == "RED")
                .ThenByDescending(x => x.QueueColor == "YELLOW")
                .ThenByDescending(x => x.PendingMinutes)
                .ThenByDescending(x => x.AssignedMinutes)
                .ToList();

            return board;
        }
    }
}
