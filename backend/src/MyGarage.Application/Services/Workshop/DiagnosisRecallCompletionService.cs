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
    public class DiagnosisRecallCompletionService : IDiagnosisRecallCompletionService
    {
        private readonly IDiagnosisStore _store;

        public DiagnosisRecallCompletionService(IDiagnosisStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public async Task<int> CloneRejectedDiagnosesVersionAsync(IEnumerable<Diagnosis> diagnoses, long actorId, string? note = null)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return 0;
            }

            static bool IsRejected(string? value) =>
                string.Equals(value, nameof(ApprovalDecisionStatusCode.REJECTED), StringComparison.OrdinalIgnoreCase);

            var rejectedDiagnoses = list
                .Where(diagnosis => IsRejected(GetTeamLeaderApprovalStatus(_store, diagnosis)) || IsRejected(GetFinalApprovalStatus(_store, diagnosis)))
                .ToList();
            if (rejectedDiagnoses.Count == 0)
            {
                return 0;
            }

            var sourceAssignmentIdsByDiagnosis = rejectedDiagnoses
                .Select(diagnosis => new { DiagnosisId = diagnosis.Id, AssignmentId = EnsureAssignmentState(_store, diagnosis).Id })
                .Where(x => x.AssignmentId > 0)
                .ToDictionary(x => x.DiagnosisId, x => x.AssignmentId);
            if (sourceAssignmentIdsByDiagnosis.Count == 0)
            {
                return 0;
            }

            var sourceAssignmentIds = sourceAssignmentIdsByDiagnosis.Values.Distinct().ToList();
            var sourceLogs = await _store.GetDiagnosisAssignmentsByIdsAndDiagnosisIdsAsync(
                sourceAssignmentIds,
                sourceAssignmentIdsByDiagnosis.Keys.ToList());

            var validSourceLogs = sourceLogs
                .Where(log => sourceAssignmentIdsByDiagnosis.TryGetValue(log.DiagnosisId, out var expectedId) && log.Id == expectedId)
                .ToList();
            if (validSourceLogs.Count == 0)
            {
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(note))
            {
                var normalizedNote = note.Trim();
                foreach (var diagnosis in rejectedDiagnoses)
                {
                    SetForemanNote(_store, diagnosis, normalizedNote);
                }
            }

            await CreateRediagnosisVersionsAsync(rejectedDiagnoses, validSourceLogs, actorId, now, "MANUAL_REDIAGNOSE_BY_TECHNICIAN");
            return validSourceLogs.Count;
        }

        public async Task<IReadOnlyList<Diagnosis>> RequeueRecalledDiagnosesAsync(
            IEnumerable<Diagnosis> recalledDiagnoses,
            long? assignedTeamId,
            long? assignedBayId,
            string? dispatchTag = null,
            string? assignmentNote = null,
            long? actorStaffId = null)
        {
            var now = VietnamTime.Now;
            var normalizedDispatchTag = NormalizeDispatchTag(dispatchTag);
            var normalizedAssignmentNote = string.IsNullOrWhiteSpace(assignmentNote) ? null : assignmentNote.Trim();

            var list = (recalledDiagnoses ?? Array.Empty<Diagnosis>()).Where(x => x != null).ToList();
            if (list.Count == 0)
            {
                return list;
            }

            foreach (var diagnosis in list)
            {
                diagnosis.Status = DiagnosisStatusPending;
                diagnosis.DispatchTag = normalizedDispatchTag ?? diagnosis.DispatchTag;
                diagnosis.AssignmentNote = normalizedAssignmentNote ?? diagnosis.AssignmentNote;
                diagnosis.DiagnosedAt = null;
                diagnosis.SubmittedAt = null;
                diagnosis.SlaDueAt = null;
                diagnosis.IsEscalated = false;

                SetTechnicianId(_store, diagnosis, null);
                SetAssistantTechnicianId(_store, diagnosis, null);
                SetAssignedTeamId(_store, diagnosis, assignedTeamId.HasValue && assignedTeamId.Value > 0 ? assignedTeamId : null);
                SetAssignedBayId(_store, diagnosis, assignedBayId.HasValue && assignedBayId.Value > 0 ? assignedBayId : null);
                SetAssignedAt(
                    _store,
                    diagnosis,
                    (assignedTeamId.HasValue && assignedTeamId.Value > 0) || (assignedBayId.HasValue && assignedBayId.Value > 0) ? now : null);

                SetTechnicianAcceptedAt(_store, diagnosis, null);
                SetTechnicianCompletedAt(_store, diagnosis, null);
                SetSupportRequestedAt(_store, diagnosis, null);
                SetSupportRequestedBy(_store, diagnosis, null);
                SetSupportRequestNote(_store, diagnosis, null);
                SetTeamLeaderApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.PENDING));
                SetFinalApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.PENDING));
                SetForemanRejectReason(_store, diagnosis, null);
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

                diagnosis.UpdatedAt = now;
                diagnosis.UpdatedBy = actorStaffId;
            }

            _store.UpdateDiagnoses(list);
            AppendDiagnosisAssignments(_store, list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventRediagnoseRequested,
                    oldStatus: DiagnosisStatusCanceled,
                    createdBy: actorStaffId,
                    note: "[RECALL_REQUEUED] Re-queued after receptionist recall")));

            await _store.SaveChangesInTransactionAsync();
            return list;
        }

        public async Task RecallDiagnosesAsync(ServiceOrder serviceOrder, IReadOnlyList<Diagnosis> activeDiagnoses)
        {
            var now = VietnamTime.Now;
            var list = activeDiagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            foreach (var diagnosis in list)
            {
                diagnosis.Status = DiagnosisStatusCanceled;
                SetCancellationStatus(_store, diagnosis, CancellationStatusApproved);
                SetCancellationReason(_store, diagnosis, GetCancellationReason(_store, diagnosis) ?? "CUSTOMER_PICKUP_CANCEL");
                SetCancellationDecisionAt(_store, diagnosis, GetCancellationDecisionAt(_store, diagnosis) ?? now);
                SetCancellationDecisionBy(_store, diagnosis, GetCancellationDecisionBy(_store, diagnosis) ?? diagnosis.UpdatedBy);
                diagnosis.UpdatedAt = now;
            }

            _store.UpdateDiagnoses(list);

            serviceOrder.Status = ServiceOrderStatusCheckedIn;
            serviceOrder.UpdatedAt = now;
            _store.UpdateServiceOrder(serviceOrder);
            AppendDiagnosisAssignments(_store, list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventCanceled,
                    oldStatus: DiagnosisStatusAssigned,
                    createdBy: diagnosis.UpdatedBy,
                    note: "Canceled because customer picked up vehicle",
                    cancelReason: GetCancellationReason(_store, diagnosis))));
            await _store.SaveChangesInTransactionAsync();
        }

        public async Task CompleteDiagnosisAsync(Diagnosis diagnosis)
        {
            var assignmentState = EnsureAssignmentState(_store, diagnosis);
            if (assignmentState.RequiresInsuranceProof && string.IsNullOrWhiteSpace(assignmentState.ProofImageUrl))
            {
                throw new InvalidOperationException("Hạng mục bảo hiểm bắt buộc ảnh nghiệm thu trước khi hoàn thành.");
            }

            var now = VietnamTime.Now;
            diagnosis.Status = DiagnosisStatusCompleted;
            diagnosis.DiagnosedAt = now;
            SetTechnicianCompletedAt(_store, diagnosis, GetTechnicianCompletedAt(_store, diagnosis) ?? now);
            SetCancellationStatus(_store, diagnosis, CancellationStatusNone);
            diagnosis.UpdatedAt = now;
            _store.UpdateDiagnosis(diagnosis);
            AppendDiagnosisAssignments(_store, new[]
            {
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventCompleted,
                    oldStatus: DiagnosisStatusAssigned,
                    createdBy: diagnosis.UpdatedBy,
                    note: "Diagnosis completed")
            });
            await _store.SaveChangesInTransactionAsync();

            var allStatuses = await _store.GetDiagnosisStatusesByServiceOrderAsync(diagnosis.ServiceOrderId);
            var isAllCompleted = allStatuses.Count > 0 && allStatuses.All(s => s == DiagnosisStatusCompleted);
            if (isAllCompleted)
            {
                var diagnoseSlots = await _store.GetActiveDiagnoseWorkSlotsForOrderAsync(diagnosis.ServiceOrderId);

                foreach (var slot in diagnoseSlots)
                {
                    slot.Status = nameof(WorkSlotStatus.DIAGNOSED_WAITING);
                    slot.SlotType = nameof(WorkSlotType.BUFFER);
                    slot.BayId = null;
                    slot.ActualReleaseTime ??= now;
                    slot.EndTime = now > slot.StartTime ? now : slot.EndTime;

                    if (string.IsNullOrWhiteSpace(slot.Notes))
                    {
                        slot.Notes = "[DIAGNOSED_WAITING_RELEASED]";
                    }
                    else if (!slot.Notes.Contains("[DIAGNOSED_WAITING_RELEASED]", StringComparison.OrdinalIgnoreCase))
                    {
                        slot.Notes += " [DIAGNOSED_WAITING_RELEASED]";
                    }
                }
            }

            await _store.SaveChangesInTransactionAsync();
        }

        public async Task<bool> AreAllDiagnosesCompletedAsync(long serviceOrderId)
        {
            var allStatus = await _store.GetDiagnosisStatusesByServiceOrderAsync(serviceOrderId);
            return allStatus.Count > 0 && allStatus.All(s => s == DiagnosisStatusCompleted);
        }

        private async Task CreateRediagnosisVersionsAsync(
            IReadOnlyList<Diagnosis> diagnoses,
            IReadOnlyList<DiagnosisAssignment> rejectedLogs,
            long actorId,
            DateTime now,
            string sourceTag)
        {
            if (diagnoses == null || diagnoses.Count == 0 || rejectedLogs == null || rejectedLogs.Count == 0)
            {
                return;
            }

            var rejectedByDiagnosisId = rejectedLogs
                .Where(x => x != null && x.DiagnosisId > 0)
                .GroupBy(x => x.DiagnosisId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

            var rediagnoseLogs = new List<DiagnosisAssignment>();
            foreach (var diagnosis in diagnoses)
            {
                if (!rejectedByDiagnosisId.TryGetValue(diagnosis.Id, out var rejectedLog))
                {
                    continue;
                }

                diagnosis.Status = DiagnosisStatusPending;
                SetTeamLeaderApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.PENDING));
                SetFinalApprovalStatus(_store, diagnosis, nameof(ApprovalDecisionStatusCode.PENDING));
                SetForemanRejectReason(_store, diagnosis, null);
                SetApprovedByForemanId(_store, diagnosis, null);
                SetApprovedByForemanAt(_store, diagnosis, null);
                SetApprovedByTeamLeaderId(_store, diagnosis, null);
                SetApprovedByTeamLeaderAt(_store, diagnosis, null);
                SetTeamLeaderNote(_store, diagnosis, null);
                SetTechnicianAcceptedAt(_store, diagnosis, null);
                SetTechnicianCompletedAt(_store, diagnosis, null);
                diagnosis.SubmittedAt = null;
                diagnosis.SlaDueAt = null;
                diagnosis.IsEscalated = false;
                diagnosis.UpdatedBy = actorId;
                diagnosis.UpdatedAt = now;

                rediagnoseLogs.Add(BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventRediagnoseRequested,
                    oldStatus: DiagnosisStatusPending,
                    createdBy: actorId,
                    note: $"[{sourceTag}] Clone from rejected assignment #{rejectedLog.Id}"));
            }

            if (rediagnoseLogs.Count == 0)
            {
                return;
            }

            _store.UpdateDiagnoses(diagnoses);
            AppendDiagnosisAssignments(_store, rediagnoseLogs);
            await _store.SaveChangesInTransactionAsync();

            var activeLogsByDiagnosisId = rediagnoseLogs
                .Where(x => x.Id > 0)
                .GroupBy(x => x.DiagnosisId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

            foreach (var diagnosis in diagnoses)
            {
                if (!rejectedByDiagnosisId.TryGetValue(diagnosis.Id, out var rejectedLog) ||
                    !activeLogsByDiagnosisId.TryGetValue(diagnosis.Id, out var activeLog))
                {
                    continue;
                }

                await CloneRecommendationsToNewAssignmentAsync(diagnosis.Id, rejectedLog.Id, activeLog.Id, actorId, now);
            }

            await _store.SaveChangesInTransactionAsync();
        }

        private async Task CloneRecommendationsToNewAssignmentAsync(
            long diagnosisId, long sourceAssignmentId, long targetAssignmentId, long actorId, DateTime now)
        {
            if (diagnosisId <= 0 || sourceAssignmentId <= 0 || targetAssignmentId <= 0)
            {
                return;
            }

            var sourceRows = await _store.GetActiveRecommendationsForAssignmentAsync(diagnosisId, sourceAssignmentId);
            if (sourceRows.Count == 0)
            {
                return;
            }

            var idMap = new Dictionary<long, long>();
            foreach (var row in sourceRows.Where(x => !x.ParentRecommendationId.HasValue))
            {
                var clone = BuildClonedRecommendation(row, diagnosisId, targetAssignmentId, null, actorId, now);
                _store.AddDiagnosisRecommendation(clone);
                await _store.SaveChangesInTransactionAsync();
                idMap[row.Id] = clone.Id;
            }

            foreach (var row in sourceRows.Where(x => x.ParentRecommendationId.HasValue))
            {
                var mappedParentId = row.ParentRecommendationId.HasValue && idMap.TryGetValue(row.ParentRecommendationId.Value, out var parentId)
                    ? parentId
                    : (long?)null;
                if (!mappedParentId.HasValue || mappedParentId.Value <= 0)
                {
                    continue;
                }

                _store.AddDiagnosisRecommendation(BuildClonedRecommendation(row, diagnosisId, targetAssignmentId, mappedParentId.Value, actorId, now));
            }
        }

        private static DiagnosisRecommendation BuildClonedRecommendation(
            DiagnosisRecommendation row, long diagnosisId, long targetAssignmentId,
            long? parentRecommendationId, long actorId, DateTime now) => new DiagnosisRecommendation
            {
                DiagnosisId = diagnosisId,
                DiagnosisAssignmentId = targetAssignmentId,
                ParentRecommendationId = parentRecommendationId,
                ItemType = row.ItemType,
                StandardServiceId = row.StandardServiceId,
                InventoryItemId = row.InventoryItemId,
                Quantity = row.Quantity,
                RecommendationStatus = string.IsNullOrWhiteSpace(row.RecommendationStatus)
                    ? nameof(RecommendationLifecycleStatusCode.ACTIVE)
                    : row.RecommendationStatus,
                SoftDeletedAt = null,
                SoftDeletedBy = null,
                Urgency = row.Urgency,
                EvidenceMediaUrls = row.EvidenceMediaUrls,
                TechnicianNotes = row.TechnicianNotes,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId
            };
    }
}
