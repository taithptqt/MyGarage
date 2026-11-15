using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisAssignmentStateHelpers;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    public class DiagnosisApprovalService : IDiagnosisApprovalService
    {
        private readonly IDiagnosisStore _store;
        private readonly IDiagnosisRecallCompletionService _recallCompletionService;

        public DiagnosisApprovalService(
            IDiagnosisStore store,
            IDiagnosisRecallCompletionService recallCompletionService)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _recallCompletionService = recallCompletionService ?? throw new ArgumentNullException(nameof(recallCompletionService));
        }

        internal DiagnosisApprovalService(IDiagnosisStore store)
            : this(store, new DiagnosisRecallCompletionService(store))
        {
        }

        public async Task RequestCancellationAsync(IEnumerable<Diagnosis> diagnoses, long requestedByStaffId, string reason)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            if (list.Any(x => x.Status != DiagnosisStatusAssigned))
            {
                throw new InvalidOperationException("Chi cho phep gui huy lenh khi dang o trang thai ASSIGNED.");
            }

            if (list.Any(x => !IsWithinGoldenCancelWindow(_store, x, now)))
            {
                throw new InvalidOperationException("To truong chi duoc gui huy trong 10 phut dau sau khi nhan xe.");
            }

            foreach (var diagnosis in list)
            {
                SetCancellationStatus(_store, diagnosis, CancellationStatusRequested);
                SetCancellationReason(_store, diagnosis, reason);
                SetCancellationRequestedAt(_store, diagnosis, now);
                SetCancellationRequestedBy(_store, diagnosis, requestedByStaffId);
                SetCancellationDecisionAt(_store, diagnosis, null);
                SetCancellationDecisionBy(_store, diagnosis, null);
                SetCancellationDecisionNote(_store, diagnosis, null);
                diagnosis.UpdatedAt = now;
            }

            _store.UpdateDiagnoses(list);
            AppendDiagnosisAssignments(_store, list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventCancellationRequested,
                    oldStatus: diagnosis.Status,
                    createdBy: requestedByStaffId,
                    note: GetCancellationDecisionNote(_store, diagnosis),
                    cancelReason: reason)));
            await _store.SaveChangesInTransactionAsync();
        }

        public async Task ApplyCancellationDecisionAsync(ServiceOrder serviceOrder, IEnumerable<Diagnosis> diagnoses, bool approve, long managerId, string? note)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            foreach (var diagnosis in list)
            {
                if (!IsCancellationPending(_store, diagnosis))
                {
                    continue;
                }

                if (approve)
                {
                    var placeholderTechnicianId = await ResolvePlaceholderTechnicianIdAsync(_store, serviceOrder, diagnosis.DispatchTag);
                    diagnosis.Status = DiagnosisStatusPending;
                    SetTechnicianId(_store, diagnosis, placeholderTechnicianId);
                    SetAssistantTechnicianId(_store, diagnosis, null);
                    SetAssignedTeamId(_store, diagnosis, null);
                    SetAssignedBayId(_store, diagnosis, null);
                    SetAssignedAt(_store, diagnosis, null);
                    SetTechnicianAcceptedAt(_store, diagnosis, null);
                    SetTechnicianCompletedAt(_store, diagnosis, null);
                    SetSupportRequestedAt(_store, diagnosis, null);
                    SetSupportRequestedBy(_store, diagnosis, null);
                    SetSupportRequestNote(_store, diagnosis, null);
                    SetApprovedByForemanId(_store, diagnosis, null);
                    SetApprovedByForemanAt(_store, diagnosis, null);
                    SetApprovedByTeamLeaderId(_store, diagnosis, null);
                    SetApprovedByTeamLeaderAt(_store, diagnosis, null);
                    SetTeamLeaderNote(_store, diagnosis, null);
                    SetForemanNote(_store, diagnosis, null);
                }

                SetCancellationStatus(_store, diagnosis, approve ? CancellationStatusApproved : CancellationStatusRejected);
                SetCancellationDecisionBy(_store, diagnosis, managerId);
                SetCancellationDecisionAt(_store, diagnosis, now);
                SetCancellationDecisionNote(_store, diagnosis, string.IsNullOrWhiteSpace(note) ? null : note.Trim());
                diagnosis.UpdatedAt = now;
            }

            if (approve)
            {
                serviceOrder.Status = ServiceOrderStatusDiagnosing;
                serviceOrder.UpdatedAt = now;
                _store.UpdateServiceOrder(serviceOrder);
            }

            _store.UpdateDiagnoses(list);
            AppendDiagnosisAssignments(_store, list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    approve ? DiagnoseLogEventCancellationApproved : DiagnoseLogEventCancellationRejected,
                    oldStatus: DiagnosisStatusAssigned,
                    createdBy: managerId,
                    note: GetCancellationDecisionNote(_store, diagnosis),
                    cancelReason: GetCancellationReason(_store, diagnosis))));
            await _store.SaveChangesInTransactionAsync();
        }

        public async Task ApproveTechnicalAsync(IEnumerable<Diagnosis> diagnoses, long teamLeaderId, string? note)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var oldStatusByDiagnosisId = list.ToDictionary(x => x.Id, x => x.Status);

            foreach (var diagnosis in list)
            {
                SetApprovedByForemanId(_store, diagnosis, teamLeaderId);
                SetApprovedByForemanAt(_store, diagnosis, now);
                SetApprovedByTeamLeaderId(_store, diagnosis, null);
                SetTeamLeaderApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.APPROVED));
                SetFinalApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.PENDING));
                diagnosis.IsEscalated = false;
                diagnosis.SubmittedAt ??= now;
                diagnosis.SlaDueAt ??= now.AddMinutes(20);
                diagnosis.Status = DiagnosisStatusAssigned;
                diagnosis.UpdatedBy = teamLeaderId;
                diagnosis.UpdatedAt = now;

                if (!string.IsNullOrWhiteSpace(note))
                {
                    SetForemanNote(_store, diagnosis, note.Trim());
                }
            }

            _store.UpdateDiagnoses(list);
            AppendDiagnosisAssignments(_store, list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventTechnicalApproved,
                    oldStatus: oldStatusByDiagnosisId.TryGetValue(diagnosis.Id, out var oldStatus) ? oldStatus : diagnosis.Status,
                    createdBy: teamLeaderId,
                    note: GetForemanNote(_store, diagnosis))));
            await _store.SaveChangesInTransactionAsync();
        }

        public async Task RejectTechnicalAsync(IEnumerable<Diagnosis> diagnoses, long teamLeaderId, string? reason)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var oldStatusByDiagnosisId = list.ToDictionary(x => x.Id, x => x.Status);

            foreach (var diagnosis in list)
            {
                diagnosis.Status = DiagnosisStatusPending;
                SetTeamLeaderApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.REJECTED));
                SetFinalApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.PENDING));
                SetForemanRejectReason(_store, diagnosis, string.IsNullOrWhiteSpace(reason) ? "TEAM_LEADER_REJECTED_REDIAGNOSE" : reason.Trim());
                SetApprovedByForemanId(_store, diagnosis, null);
                SetApprovedByForemanAt(_store, diagnosis, null);
                SetApprovedByTeamLeaderId(_store, diagnosis, null);
                SetApprovedByTeamLeaderAt(_store, diagnosis, null);
                SetTechnicianCompletedAt(_store, diagnosis, null);
                diagnosis.UpdatedBy = teamLeaderId;
                diagnosis.UpdatedAt = now;
            }

            _store.UpdateDiagnoses(list);
            var rejectedLogs = list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventTechnicalRejected,
                    oldStatus: oldStatusByDiagnosisId.TryGetValue(diagnosis.Id, out var oldStatus) ? oldStatus : diagnosis.Status,
                    createdBy: teamLeaderId,
                    note: GetForemanRejectReason(_store, diagnosis)))
                .ToList();
            AppendDiagnosisAssignments(_store, rejectedLogs);
            await _store.SaveChangesInTransactionAsync();
        }

        public async Task ApproveFinalAsync(IEnumerable<Diagnosis> diagnoses, long approverId, string? note, bool bypassTeamLeader = false, string? bypassAuditNote = null)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var oldStatusByDiagnosisId = list.ToDictionary(x => x.Id, x => x.Status);

            foreach (var diagnosis in list)
            {
                var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                if (bypassTeamLeader &&
                    !string.Equals(GetTeamLeaderApprovalStatus(_store, diagnosis), nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase))
                {
                    SetTeamLeaderApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.BYPASSED));
                    var normalizedAudit = string.IsNullOrWhiteSpace(bypassAuditNote) ? null : bypassAuditNote.Trim();
                    normalizedNote = string.IsNullOrWhiteSpace(normalizedAudit)
                        ? normalizedNote
                        : (string.IsNullOrWhiteSpace(normalizedNote) ? normalizedAudit : $"{normalizedAudit} | {normalizedNote}");
                }

                SetApprovedByTeamLeaderId(_store, diagnosis, approverId);
                SetApprovedByTeamLeaderAt(_store, diagnosis, now);
                SetTeamLeaderNote(_store, diagnosis, normalizedNote);
                SetFinalApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.APPROVED));
                diagnosis.Status = DiagnosisStatusCompleted;
                diagnosis.DiagnosedAt ??= now;
                SetTechnicianCompletedAt(_store, diagnosis, GetTechnicianCompletedAt(_store, diagnosis) ?? now);
                SetForemanRejectReason(_store, diagnosis, null);
                diagnosis.UpdatedBy = approverId;
                diagnosis.UpdatedAt = now;
            }

            _store.UpdateDiagnoses(list);
            var serviceOrderId = list[0].ServiceOrderId;
            var order = await _store.GetServiceOrderByIdAsync(serviceOrderId);
            if (order != null)
            {
                order.Status = nameof(ServiceOrderLifecycleStatus.QUOTING);
                order.UpdatedAt = now;
                _store.UpdateServiceOrder(order);
            }
            AppendDiagnosisAssignments(_store, list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    bypassTeamLeader ? DiagnoseLogEventFinalApprovedBypass : DiagnoseLogEventFinalApproved,
                    oldStatus: oldStatusByDiagnosisId.TryGetValue(diagnosis.Id, out var oldStatus) ? oldStatus : diagnosis.Status,
                    createdBy: approverId,
                    note: bypassTeamLeader
                        ? (string.IsNullOrWhiteSpace(bypassAuditNote) ? GetTeamLeaderNote(_store, diagnosis) : bypassAuditNote)
                        : GetTeamLeaderNote(_store, diagnosis))));
            await _store.SaveChangesInTransactionAsync();
        }

        public async Task RejectFinalAsync(IEnumerable<Diagnosis> diagnoses, long foremanId, string? note)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var oldStatusByDiagnosisId = list.ToDictionary(x => x.Id, x => x.Status);

            foreach (var diagnosis in list)
            {
                diagnosis.Status = DiagnosisStatusAssigned;
                SetFinalApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.REJECTED));
                SetForemanRejectReason(_store, diagnosis, string.IsNullOrWhiteSpace(note) ? "REJECTED_REDIAGNOSE" : note.Trim());
                diagnosis.UpdatedBy = foremanId;
                diagnosis.UpdatedAt = now;
            }

            _store.UpdateDiagnoses(list);
            var rejectedLogs = list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventFinalRejected,
                    oldStatus: oldStatusByDiagnosisId.TryGetValue(diagnosis.Id, out var oldStatus) ? oldStatus : diagnosis.Status,
                    createdBy: foremanId,
                    note: GetForemanRejectReason(_store, diagnosis)))
                .ToList();
            AppendDiagnosisAssignments(_store, rejectedLogs);
            await _store.SaveChangesInTransactionAsync();
        }

        public Task<int> CloneRejectedDiagnosesVersionAsync(IEnumerable<Diagnosis> diagnoses, long actorId, string? note = null) =>
            _recallCompletionService.CloneRejectedDiagnosesVersionAsync(diagnoses, actorId, note);

        public Task RecallDiagnosesAsync(ServiceOrder serviceOrder, IReadOnlyList<Diagnosis> activeDiagnoses) =>
            _recallCompletionService.RecallDiagnosesAsync(serviceOrder, activeDiagnoses);

        public Task<IReadOnlyList<Diagnosis>> RequeueRecalledDiagnosesAsync(
            IEnumerable<Diagnosis> recalledDiagnoses,
            long? assignedTeamId,
            long? assignedBayId,
            string? dispatchTag = null,
            string? assignmentNote = null,
            long? actorStaffId = null) =>
            _recallCompletionService.RequeueRecalledDiagnosesAsync(
                recalledDiagnoses,
                assignedTeamId,
                assignedBayId,
                dispatchTag,
                assignmentNote,
                actorStaffId);

        public Task CompleteDiagnosisAsync(Diagnosis diagnosis) =>
            _recallCompletionService.CompleteDiagnosisAsync(diagnosis);

        public Task<bool> AreAllDiagnosesCompletedAsync(long serviceOrderId) =>
            _recallCompletionService.AreAllDiagnosesCompletedAsync(serviceOrderId);
    }
}
