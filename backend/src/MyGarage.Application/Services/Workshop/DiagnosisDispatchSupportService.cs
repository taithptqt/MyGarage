using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisAssignmentStateHelpers;
using static MyGarage.Services.DiagnosisDispatchCollaborationHelpers;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    public class DiagnosisDispatchSupportService : IDiagnosisDispatchSupportService
    {
        private readonly IDiagnosisStore _store;

        public DiagnosisDispatchSupportService(IDiagnosisStore store)
        {
            _store = store;
        }

        public async Task MarkSupportRequestedAsync(
            IEnumerable<Diagnosis> diagnoses,
            long requestedByStaffId,
            string? note,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto>? supportingAssignments = null)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var latest = list
                .OrderByDescending(x => x.UpdatedAt ?? GetAssignedAt(_store, x) ?? x.CreatedAt ?? DateTime.MinValue)
                .FirstOrDefault();
            var existingMeta = latest == null
                ? null
                : TryParseDispatchCollaborationNote(null, GetForemanNote(_store, latest));

            long? primaryTeamId = existingMeta?.PrimaryTeamId;
            if (!primaryTeamId.HasValue || primaryTeamId.Value <= 0)
            {
                primaryTeamId = list
                    .Select(x => GetAssignedTeamId(_store, x))
                    .Where(x => x.HasValue && x.Value > 0)
                    .FirstOrDefault();
            }
            if (!primaryTeamId.HasValue || primaryTeamId.Value <= 0)
            {
                primaryTeamId = null;
            }

            var requestedAssignments = NormalizeSupportingAssignments(
                supportingAssignments,
                null,
                primaryTeamId);
            var requestedByTeam = requestedAssignments
                .Where(x => x.TeamId > 0)
                .ToDictionary(x => x.TeamId, x => x);

            var mergedByTeam = new Dictionary<long, DispatchCollaborationTeamAssignmentMeta>();
            foreach (var existing in existingMeta?.TeamAssignments ?? new List<DispatchCollaborationTeamAssignmentMeta>())
            {
                if (existing == null || existing.TeamId <= 0)
                {
                    continue;
                }

                if (primaryTeamId.HasValue && existing.TeamId == primaryTeamId.Value)
                {
                    continue;
                }

                mergedByTeam[existing.TeamId] = new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = existing.TeamId,
                    TechnicianId = existing.TechnicianId,
                    AssistantTechnicianId = existing.AssistantTechnicianId,
                    RequestedTechnicianCount = NormalizeRequestedTechnicianCount(existing.RequestedTechnicianCount)
                };
            }

            foreach (var requested in requestedByTeam.Values)
            {
                var existingRequestedCount = mergedByTeam.TryGetValue(requested.TeamId, out var existing)
                    ? existing.RequestedTechnicianCount
                    : null;

                mergedByTeam[requested.TeamId] = new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = requested.TeamId,
                    TechnicianId = null,
                    AssistantTechnicianId = null,
                    RequestedTechnicianCount = NormalizeRequestedTechnicianCount(
                        requested.RequestedTechnicianCount
                        ?? existingRequestedCount
                        ?? 1)
                };
            }

            var collaborationAssignments = new List<DispatchCollaborationTeamAssignmentMeta>();
            if (primaryTeamId.HasValue && primaryTeamId.Value > 0)
            {
                var primaryDiagnosis = list
                    .Where(x => GetAssignedTeamId(_store, x) == primaryTeamId.Value)
                    .OrderByDescending(x => GetAssignedAt(_store, x) ?? x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                    .FirstOrDefault();

                collaborationAssignments.Add(new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = primaryTeamId.Value,
                    TechnicianId = primaryDiagnosis != null && GetTechnicianId(_store, primaryDiagnosis).GetValueOrDefault() > 0 ? GetTechnicianId(_store, primaryDiagnosis) : null,
                    AssistantTechnicianId = primaryDiagnosis != null ? GetAssistantTechnicianId(_store, primaryDiagnosis) : null,
                    RequestedTechnicianCount = null
                });
            }

            collaborationAssignments.AddRange(mergedByTeam.Values);
            _ = BuildDispatchCollaborationMeta(primaryTeamId, collaborationAssignments);

            foreach (var diagnosis in list)
            {
                if (diagnosis.Status == DiagnosisStatusCompleted || diagnosis.Status == DiagnosisStatusCanceled)
                {
                    continue;
                }

                SetSupportRequestedAt(_store, diagnosis, now);
                SetSupportRequestedBy(_store, diagnosis, requestedByStaffId);
                SetSupportRequestNote(_store, diagnosis, string.IsNullOrWhiteSpace(note) ? null : note.Trim());
                diagnosis.UpdatedAt = now;
            }

            var clones = new List<Diagnosis>();
            foreach (var assignment in requestedAssignments)
            {
                if (assignment.TeamId <= 0)
                {
                    continue;
                }

                var seed = list
                    .Where(x => x.Status != DiagnosisStatusCompleted && x.Status != DiagnosisStatusCanceled)
                    .OrderByDescending(x => x.UpdatedAt ?? GetAssignedAt(_store, x) ?? x.CreatedAt ?? DateTime.MinValue)
                    .FirstOrDefault();
                if (seed == null)
                {
                    continue;
                }

                clones.Add(new Diagnosis
                {
                    ServiceOrderId = seed.ServiceOrderId,
                    Symptom = seed.Symptom,
                    RootCause = seed.RootCause,
                    Severity = seed.Severity,
                    Status = DiagnosisStatusPending,
                    DispatchTag = seed.DispatchTag,
                    DiagnosedAt = null,
                    CreatedAt = now,
                    UpdatedAt = now,
                    UpdatedBy = requestedByStaffId
                });

                var clone = clones[^1];
                SetTechnicianId(_store, clone, null);
                SetAssistantTechnicianId(_store, clone, null);
                SetAssignedBayId(_store, clone, GetAssignedBayId(_store, seed));
                SetAssignedTeamId(_store, clone, assignment.TeamId);
                SetAssignedAt(_store, clone, null);
                SetTechnicianAcceptedAt(_store, clone, null);
                SetTechnicianCompletedAt(_store, clone, null);
                SetSupportRequestedAt(_store, clone, now);
                SetSupportRequestedBy(_store, clone, requestedByStaffId);
                SetSupportRequestNote(_store, clone, string.IsNullOrWhiteSpace(note) ? null : note.Trim());
                SetTeamLeaderApprovalStatus(_store, clone, nameof(ApprovalDecisionStatusCode.PENDING));
                SetFinalApprovalStatus(_store, clone, nameof(ApprovalDecisionStatusCode.PENDING));
                SetForemanRejectReason(_store, clone, null);
                SetApprovedByForemanId(_store, clone, null);
                SetApprovedByForemanAt(_store, clone, null);
                SetApprovedByTeamLeaderId(_store, clone, null);
                SetApprovedByTeamLeaderAt(_store, clone, null);
                SetTeamLeaderNote(_store, clone, null);
                SetForemanNote(_store, clone, null);
                SetCancellationStatus(_store, clone, CancellationStatusNone);
                SetCancellationReason(_store, clone, null);
                SetCancellationRequestedAt(_store, clone, null);
                SetCancellationRequestedBy(_store, clone, null);
                SetCancellationDecisionAt(_store, clone, null);
                SetCancellationDecisionBy(_store, clone, null);
                SetCancellationDecisionNote(_store, clone, null);
            }

            if (clones.Count > 0)
            {
                _store.AddDiagnoses(clones);
            }

            _store.UpdateDiagnoses(list);
            await _store.SaveChangesInTransactionAsync();

            var supportLogs = new List<DiagnosisAssignment>();
            supportLogs.AddRange(list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventSupportRequested,
                    oldStatus: diagnosis.Status,
                    createdBy: requestedByStaffId,
                    note: GetSupportRequestNote(_store, diagnosis))));

            supportLogs.AddRange(clones.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventCreated,
                    oldStatus: null,
                    createdBy: requestedByStaffId,
                    note: "Created supporting-team diagnosis clone")));

            AppendDiagnosisAssignments(_store, supportLogs);
            await _store.SaveChangesInTransactionAsync();
        }

        public async Task AssignSupportingTeamSelectionAsync(
            IEnumerable<Diagnosis> diagnoses,
            long teamId,
            long teamLeaderId,
            long technicianId,
            long? assistantTechnicianId,
            string? note)
        {
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var latest = list
                .OrderByDescending(x => x.UpdatedAt ?? GetAssignedAt(_store, x) ?? x.CreatedAt ?? DateTime.MinValue)
                .FirstOrDefault();
            var collaborationMeta = latest == null
                ? null
                : TryParseDispatchCollaborationNote(null, GetForemanNote(_store, latest));
            if (collaborationMeta == null)
            {
                throw new InvalidOperationException("Không tìm thấy thông tin phối hợp để cập nhật nhân sự hỗ trợ.");
            }

            var primaryTeamId = collaborationMeta.PrimaryTeamId;
            if (primaryTeamId.HasValue && primaryTeamId.Value == teamId)
            {
                throw new InvalidOperationException("Tổ chính không được dùng luồng chọn nhân sự hỗ trợ tổ phụ.");
            }

            if (!collaborationMeta.TeamIds.Contains(teamId))
            {
                throw new InvalidOperationException("Tổ được chọn không nằm trong danh sách yêu cầu phối hợp.");
            }

            var byTeam = (collaborationMeta.TeamAssignments ?? new List<DispatchCollaborationTeamAssignmentMeta>())
                .Where(x => x != null && x.TeamId > 0)
                .GroupBy(x => x.TeamId)
                .ToDictionary(x => x.Key, x => x.First());

            if (!byTeam.TryGetValue(teamId, out var targetAssignment))
            {
                targetAssignment = new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = teamId,
                    TechnicianId = null,
                    AssistantTechnicianId = null,
                    RequestedTechnicianCount = null
                };
            }

            targetAssignment.TechnicianId = technicianId > 0 ? technicianId : null;
            targetAssignment.AssistantTechnicianId = assistantTechnicianId.HasValue && assistantTechnicianId.Value > 0
                ? assistantTechnicianId
                : null;
            if (targetAssignment.AssistantTechnicianId.HasValue && targetAssignment.AssistantTechnicianId == targetAssignment.TechnicianId)
            {
                targetAssignment.AssistantTechnicianId = null;
            }
            targetAssignment.RequestedTechnicianCount = NormalizeRequestedTechnicianCount(targetAssignment.RequestedTechnicianCount ?? 1);
            byTeam[teamId] = targetAssignment;

            var orderedAssignments = byTeam
                .Values
                .OrderBy(x => x.TeamId)
                .ToList();
            _ = BuildDispatchCollaborationMeta(primaryTeamId, orderedAssignments);

            var hasPendingSupportSelection = orderedAssignments.Any(x =>
                (!primaryTeamId.HasValue || x.TeamId != primaryTeamId.Value) &&
                NormalizeRequestedTechnicianCount(x.RequestedTechnicianCount).GetValueOrDefault() > 0 &&
                (!x.TechnicianId.HasValue || x.TechnicianId.Value <= 0));

            var now = VietnamTime.Now;
            foreach (var diagnosis in list)
            {
                if (diagnosis.Status == DiagnosisStatusCompleted || diagnosis.Status == DiagnosisStatusCanceled)
                {
                    continue;
                }

                diagnosis.UpdatedBy = teamLeaderId;
                diagnosis.UpdatedAt = now;

                if (!string.IsNullOrWhiteSpace(note))
                {
                    SetSupportRequestNote(_store, diagnosis, note.Trim());
                }

                if (!hasPendingSupportSelection)
                {
                    SetSupportRequestedAt(_store, diagnosis, null);
                    SetSupportRequestedBy(_store, diagnosis, null);
                }
            }

            _store.UpdateDiagnoses(list);
            AppendDiagnosisAssignments(_store, list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventSupportSelection,
                    oldStatus: diagnosis.Status,
                    createdBy: teamLeaderId,
                    note: note)));
            await _store.SaveChangesInTransactionAsync();
        }
    }
}
