using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisAssignmentStateHelpers;
using static MyGarage.Services.DiagnosisDispatchCollaborationHelpers;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    public class DiagnosisDispatchService : IDiagnosisDispatchService
    {
        private readonly IDiagnosisStore _store;
        private readonly IDiagnosisDispatchBoardService _boardService;
        private readonly IDiagnosisDispatchSupportService _supportService;

        public DiagnosisDispatchService(
            IDiagnosisStore store,
            IDiagnosisDispatchBoardService boardService,
            IDiagnosisDispatchSupportService supportService)
        {
            _store = store;
            _boardService = boardService;
            _supportService = supportService;
        }

        public async Task AssignDiagnosesAsync(
            IEnumerable<Diagnosis> diagnoses,
            long technicianId,
            long? assistantTechnicianId,
            long? teamId,
            IReadOnlyList<long>? supportingTeamIds,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto>? supportingAssignments,
            long? bayId,
            bool managerOverride)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }
            var statusBeforeByDiagnosisId = list.ToDictionary(x => x.Id, x => x.Status);

            var resolvedPrimaryTeamId = teamId ?? await _store.ResolveTeamIdForTechnicianAsync(technicianId);
            var normalizedSupportingAssignments = NormalizeSupportingAssignments(
                supportingAssignments,
                supportingTeamIds,
                resolvedPrimaryTeamId);

            var supportingTeamIdsResolved = normalizedSupportingAssignments
                .Select(x => x.TeamId)
                .Distinct()
                .ToList();

            var technicianByTeam = new Dictionary<long, long>();
            var assistantByTeam = new Dictionary<long, long?>();
            var collaborationAssignments = new List<DispatchCollaborationTeamAssignmentMeta>();

            if (resolvedPrimaryTeamId.HasValue && resolvedPrimaryTeamId.Value > 0)
            {
                technicianByTeam[resolvedPrimaryTeamId.Value] = technicianId;
                assistantByTeam[resolvedPrimaryTeamId.Value] = assistantTechnicianId;
                collaborationAssignments.Add(new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = resolvedPrimaryTeamId.Value,
                    TechnicianId = technicianId,
                    AssistantTechnicianId = assistantTechnicianId
                });
            }

            foreach (var supportingAssignment in normalizedSupportingAssignments)
            {
                var resolvedSupportLead = supportingAssignment.TechnicianId;
                var supportAssistant = supportingAssignment.AssistantTechnicianId;
                if (resolvedSupportLead.HasValue && supportAssistant.HasValue && supportAssistant.Value == resolvedSupportLead.Value)
                {
                    supportAssistant = null;
                }

                if (resolvedSupportLead.HasValue && resolvedSupportLead.Value > 0)
                {
                    technicianByTeam[supportingAssignment.TeamId] = resolvedSupportLead.Value;
                    assistantByTeam[supportingAssignment.TeamId] = supportAssistant;
                }

                collaborationAssignments.Add(new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = supportingAssignment.TeamId,
                    TechnicianId = resolvedSupportLead,
                    AssistantTechnicianId = supportAssistant,
                    RequestedTechnicianCount = supportingAssignment.RequestedTechnicianCount
                });
            }

            var collaborationTeamIds = new List<long>();
            if (resolvedPrimaryTeamId.HasValue && resolvedPrimaryTeamId.Value > 0)
            {
                collaborationTeamIds.Add(resolvedPrimaryTeamId.Value);
            }
            collaborationTeamIds.AddRange(supportingTeamIdsResolved);
            collaborationTeamIds = collaborationTeamIds.Distinct().ToList();

            var assignmentTeamIds = technicianByTeam
                .Keys
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var collaborationByDiagnosis = assignmentTeamIds.Count == 0
                ? new List<long?> { resolvedPrimaryTeamId }
                : assignmentTeamIds.Select(x => (long?)x).ToList();

            _ = BuildDispatchCollaborationMeta(resolvedPrimaryTeamId, collaborationAssignments);

            var sortedDiagnoses = list
                .OrderBy(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ThenBy(x => x.Id)
                .ToList();

            for (var index = 0; index < sortedDiagnoses.Count; index++)
            {
                var diagnosis = sortedDiagnoses[index];
                var assignedTeamId = collaborationByDiagnosis[index % collaborationByDiagnosis.Count];

                var resolvedLeadTechnicianId = assignedTeamId.HasValue && technicianByTeam.TryGetValue(assignedTeamId.Value, out var mappedTechnicianId)
                    ? mappedTechnicianId
                    : technicianId;
                var resolvedAssistantTechnicianId = assignedTeamId.HasValue && assistantByTeam.TryGetValue(assignedTeamId.Value, out var mappedAssistantTechnicianId)
                    ? mappedAssistantTechnicianId
                    : (resolvedPrimaryTeamId.HasValue && assignedTeamId == resolvedPrimaryTeamId ? assistantTechnicianId : null);
                if (resolvedAssistantTechnicianId.HasValue && resolvedAssistantTechnicianId.Value == resolvedLeadTechnicianId)
                {
                    resolvedAssistantTechnicianId = null;
                }

                SetTechnicianId(_store, diagnosis, resolvedLeadTechnicianId);
                SetAssistantTechnicianId(_store, diagnosis, resolvedAssistantTechnicianId);
                diagnosis.Status = DiagnosisStatusAssigned;
                SetAssignedTeamId(_store, diagnosis, assignedTeamId);
                // Keep existing bay assignment when the caller does not explicitly send a bay.
                SetAssignedBayId(_store, diagnosis, bayId.HasValue && bayId.Value > 0
                    ? bayId
                    : GetAssignedBayId(_store, diagnosis));
                SetAssignedAt(_store, diagnosis, GetAssignedAt(_store, diagnosis) ?? now);
                SetTechnicianAcceptedAt(_store, diagnosis, null);
                SetTechnicianCompletedAt(_store, diagnosis, null);

                SetApprovedByForemanId(_store, diagnosis, null);
                SetApprovedByForemanAt(_store, diagnosis, null);
                SetApprovedByTeamLeaderId(_store, diagnosis, null);
                SetApprovedByTeamLeaderAt(_store, diagnosis, null);
                SetTeamLeaderNote(_store, diagnosis, null);
                SetForemanNote(_store, diagnosis, null);

                SetCancellationStatus(_store, diagnosis, CancellationStatusNone);
                SetCancellationReason(_store, diagnosis, null);
                SetCancellationRequestedAt(_store, diagnosis, null);
                SetCancellationRequestedBy(_store, diagnosis, null);
                SetCancellationDecisionAt(_store, diagnosis, null);
                SetCancellationDecisionBy(_store, diagnosis, null);
                SetCancellationDecisionNote(_store, diagnosis, null);

                if (managerOverride)
                {
                    SetSupportRequestedAt(_store, diagnosis, null);
                    SetSupportRequestedBy(_store, diagnosis, null);
                    SetSupportRequestNote(_store, diagnosis, null);
                }

                diagnosis.UpdatedAt = now;
            }

            _store.UpdateDiagnoses(list);
            AppendDiagnosisAssignments(_store, list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventAssigned,
                    oldStatus: statusBeforeByDiagnosisId.TryGetValue(diagnosis.Id, out var previousStatus) ? previousStatus : null,
                    createdBy: technicianId,
                    note: managerOverride ? "Assigned by manager override" : "Assigned diagnosis")));
            await _store.SaveChangesInTransactionAsync();
        }

        public async Task StartDiagnosesAsync(IEnumerable<Diagnosis> diagnoses, long technicianId, long? bayId)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }
            var statusBeforeByDiagnosisId = list.ToDictionary(x => x.Id, x => x.Status);

            foreach (var diagnosis in list)
            {
                if (diagnosis.Status == DiagnosisStatusCompleted || diagnosis.Status == DiagnosisStatusCanceled)
                {
                    continue;
                }

                diagnosis.Status = DiagnosisStatusAssigned;
                if (GetTechnicianId(_store, diagnosis) == technicianId)
                {
                    SetTechnicianAcceptedAt(_store, diagnosis, GetTechnicianAcceptedAt(_store, diagnosis) ?? now);
                }
                SetAssignedAt(_store, diagnosis, GetAssignedAt(_store, diagnosis) ?? now);
                SetAssignedBayId(_store, diagnosis, bayId ?? GetAssignedBayId(_store, diagnosis));
                diagnosis.UpdatedBy = technicianId;
                diagnosis.UpdatedAt = now;
            }

            var serviceOrderIds = list
                .Select(x => x.ServiceOrderId)
                .Distinct()
                .ToList();
            if (serviceOrderIds.Count > 0)
            {
                var diagnoseSlots = await _store.GetDiagnoseWorkSlotsForOrdersAsync(serviceOrderIds);

                foreach (var slot in diagnoseSlots)
                {
                    if (bayId.HasValue && bayId.Value > 0)
                    {
                        slot.BayId = bayId.Value;
                    }

                    slot.Status = nameof(WorkSlotStatus.IN_PROGRESS);
                    slot.ActualStartTime ??= now;
                    slot.TechnicianId ??= technicianId;
                }
            }

            _store.UpdateDiagnoses(list);
            AppendDiagnosisAssignments(_store, list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventStarted,
                    oldStatus: statusBeforeByDiagnosisId.TryGetValue(diagnosis.Id, out var previousStatus) ? previousStatus : null,
                    createdBy: technicianId,
                    note: "Lead technician accepted diagnosis")));
            await _store.SaveChangesInTransactionAsync();
        }

        public Task MarkSupportRequestedAsync(
            IEnumerable<Diagnosis> diagnoses,
            long requestedByStaffId,
            string? note,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto>? supportingAssignments = null)
            => _supportService.MarkSupportRequestedAsync(diagnoses, requestedByStaffId, note, supportingAssignments);

        public Task AssignSupportingTeamSelectionAsync(
            IEnumerable<Diagnosis> diagnoses,
            long teamId,
            long teamLeaderId,
            long technicianId,
            long? assistantTechnicianId,
            string? note)
            => _supportService.AssignSupportingTeamSelectionAsync(diagnoses, teamId, teamLeaderId, technicianId, assistantTechnicianId, note);

        public Task<IReadOnlyList<DiagnosisDispatchBoardItemDto>> GetDispatchBoardAsync(
            long? branchId,
            long? teamId,
            bool includeCompleted,
            string? viewMode = null)
            => _boardService.GetDispatchBoardAsync(branchId, teamId, includeCompleted, viewMode);
    }
}
