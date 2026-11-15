using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisAssignmentStateHelpers;
using static MyGarage.Services.DiagnosisRoutingHelpers;
using static MyGarage.Services.DiagnosisServiceHelpers;

namespace MyGarage.Services
{
    public class DiagnosisService : GenericService<Diagnosis>, IDiagnosisService
    {
        private const string LogTypeCheckIn = "CHECK_IN";

        private readonly IDiagnosisStore _store;
        private readonly IDiagnosisIntakeHandoverService _intakeHandoverService;
        private readonly IDiagnosisRoutingService _routingService;
        private readonly IDiagnosisDispatchService _dispatchService;
        private readonly IDiagnosisApprovalService _approvalService;

        public DiagnosisService(
            IDiagnosisRepository repository,
            IDiagnosisStore store,
            IDiagnosisIntakeHandoverService intakeHandoverService,
            IDiagnosisRoutingService routingService,
            IDiagnosisDispatchService dispatchService,
            IDiagnosisApprovalService approvalService) : base(repository)
        {
            _store = store;
            _intakeHandoverService = intakeHandoverService;
            _routingService = routingService;
            _dispatchService = dispatchService;
            _approvalService = approvalService;
        }

        // --- Approvals ---

        public Task RequestCancellationAsync(IEnumerable<Diagnosis> diagnoses, long requestedByStaffId, string reason) =>
            _approvalService.RequestCancellationAsync(diagnoses, requestedByStaffId, reason);

        public Task ApplyCancellationDecisionAsync(ServiceOrder serviceOrder, IEnumerable<Diagnosis> diagnoses, bool approve, long managerId, string? note) =>
            _approvalService.ApplyCancellationDecisionAsync(serviceOrder, diagnoses, approve, managerId, note);

        public Task ApproveTechnicalAsync(IEnumerable<Diagnosis> diagnoses, long teamLeaderId, string? note) =>
            _approvalService.ApproveTechnicalAsync(diagnoses, teamLeaderId, note);

        public Task RejectTechnicalAsync(IEnumerable<Diagnosis> diagnoses, long teamLeaderId, string? reason) =>
            _approvalService.RejectTechnicalAsync(diagnoses, teamLeaderId, reason);

        public Task ApproveFinalAsync(IEnumerable<Diagnosis> diagnoses, long approverId, string? note, bool bypassTeamLeader = false, string? bypassAuditNote = null) =>
            _approvalService.ApproveFinalAsync(diagnoses, approverId, note, bypassTeamLeader, bypassAuditNote);

        public Task RejectFinalAsync(IEnumerable<Diagnosis> diagnoses, long foremanId, string? note) =>
            _approvalService.RejectFinalAsync(diagnoses, foremanId, note);

        public Task<int> CloneRejectedDiagnosesVersionAsync(IEnumerable<Diagnosis> diagnoses, long actorId, string? note = null) =>
            _approvalService.CloneRejectedDiagnosesVersionAsync(diagnoses, actorId, note);

        public Task RecallDiagnosesAsync(ServiceOrder serviceOrder, IReadOnlyList<Diagnosis> activeDiagnoses) =>
            _approvalService.RecallDiagnosesAsync(serviceOrder, activeDiagnoses);

        public Task<IReadOnlyList<Diagnosis>> RequeueRecalledDiagnosesAsync(
            IEnumerable<Diagnosis> recalledDiagnoses,
            long? assignedTeamId,
            long? assignedBayId,
            string? dispatchTag = null,
            string? assignmentNote = null,
            long? actorStaffId = null) =>
            _approvalService.RequeueRecalledDiagnosesAsync(recalledDiagnoses, assignedTeamId, assignedBayId, dispatchTag, assignmentNote, actorStaffId);

        public Task CompleteDiagnosisAsync(Diagnosis diagnosis) =>
            _approvalService.CompleteDiagnosisAsync(diagnosis);

        public Task<bool> AreAllDiagnosesCompletedAsync(long serviceOrderId) =>
            _approvalService.AreAllDiagnosesCompletedAsync(serviceOrderId);

        // --- Assignment State ---

        public long? ReadAssignedTeamId(Diagnosis diagnosis) => GetAssignedTeamId(_store, diagnosis);
        public long? ReadTechnicianId(Diagnosis diagnosis) => GetTechnicianId(_store, diagnosis);
        public long? ReadAssistantTechnicianId(Diagnosis diagnosis) => GetAssistantTechnicianId(_store, diagnosis);
        public long? ReadAssignedBayId(Diagnosis diagnosis) => GetAssignedBayId(_store, diagnosis);
        public DateTime? ReadCancellationRequestedAt(Diagnosis diagnosis) => GetCancellationRequestedAt(_store, diagnosis);
        public DateTime? ReadCancellationDecisionAt(Diagnosis diagnosis) => GetCancellationDecisionAt(_store, diagnosis);
        public string? ReadTeamLeaderApprovalStatus(Diagnosis diagnosis) => GetTeamLeaderApprovalStatus(_store, diagnosis);
        public string? ReadFinalApprovalStatus(Diagnosis diagnosis) => GetFinalApprovalStatus(_store, diagnosis);
        public string? ReadForemanRejectReason(Diagnosis diagnosis) => GetForemanRejectReason(_store, diagnosis);
        public long? ReadApprovedByForemanId(Diagnosis diagnosis) => GetApprovedByForemanId(_store, diagnosis);
        public DateTime? ReadApprovedByForemanAt(Diagnosis diagnosis) => GetApprovedByForemanAt(_store, diagnosis);
        public long? ReadApprovedByTeamLeaderId(Diagnosis diagnosis) => GetApprovedByTeamLeaderId(_store, diagnosis);
        public DateTime? ReadApprovedByTeamLeaderAt(Diagnosis diagnosis) => GetApprovedByTeamLeaderAt(_store, diagnosis);
        public string? ReadTeamLeaderNote(Diagnosis diagnosis) => GetTeamLeaderNote(_store, diagnosis);
        public string? ReadForemanNote(Diagnosis diagnosis) => GetForemanNote(_store, diagnosis);
        public bool ReadRequiresInsuranceProof(Diagnosis diagnosis) => EnsureAssignmentState(_store, diagnosis).RequiresInsuranceProof;
        public string? ReadProofImageUrl(Diagnosis diagnosis) => EnsureAssignmentState(_store, diagnosis).ProofImageUrl;

        public long? ReadDiagnosisAssignmentId(Diagnosis diagnosis)
        {
            var assignmentId = EnsureAssignmentState(_store, diagnosis).Id;
            return assignmentId > 0 ? assignmentId : null;
        }

        public int ReadTeamLeaderApprovalPendingMinutes(Diagnosis diagnosis, DateTime? now = null) =>
            CalculateTeamLeaderApprovalPendingMinutes(diagnosis, now);

        public bool IsTeamLeaderApprovalWarning(Diagnosis diagnosis, long? executeBranchId = null, DateTime? now = null) =>
            !IsTeamLeaderApprovalEscalated(diagnosis, executeBranchId, now) &&
            IsTeamLeaderApprovalPending(_store, diagnosis) &&
            ReadTeamLeaderApprovalPendingMinutes(diagnosis, now) >= GetTeamLeaderApprovalWarningMinutes(_store, executeBranchId);

        public bool IsTeamLeaderApprovalEscalated(Diagnosis diagnosis, long? executeBranchId = null, DateTime? now = null) =>
            IsTeamLeaderApprovalPending(_store, diagnosis) &&
            ReadTeamLeaderApprovalPendingMinutes(diagnosis, now) >= GetTeamLeaderApprovalEscalationMinutes(_store, executeBranchId);

        // --- Bay & Queue Routing ---

        public Task<WorkSlot?> ScheduleDiagnosisQueueSlotAsync(long serviceOrderId, int diagnoseMinutes, int bufferMinutes = 15, long? preferredBayId = null) =>
            _routingService.ScheduleDiagnosisQueueSlotAsync(serviceOrderId, diagnoseMinutes, bufferMinutes, preferredBayId);

        public Task<bool> IsDispatchTagCompatibleAsync(string? dispatchTag, long? teamId) =>
            _routingService.IsDispatchTagCompatibleAsync(dispatchTag, teamId);

        public Task<(bool IsCompatible, string Reason)> ValidateTeamBayCompatibilityAsync(long serviceOrderId, long? teamId, long bayId) =>
            _routingService.ValidateTeamBayCompatibilityAsync(serviceOrderId, teamId, bayId);

        public Task<bool> IsBayInBranchAsync(long bayId, long branchId) =>
            _routingService.IsBayInBranchAsync(bayId, branchId);

        public Task<long?> ResolveDiagnoseBayIdForBranchAsync(long branchId, long? preferredBayId = null) =>
            _routingService.ResolveDiagnoseBayIdForBranchAsync(branchId, preferredBayId);

        // --- Dispatch ---

        public Task AssignDiagnosesAsync(
            IEnumerable<Diagnosis> diagnoses,
            long technicianId,
            long? assistantTechnicianId,
            long? teamId,
            IReadOnlyList<long>? supportingTeamIds,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto>? supportingAssignments,
            long? bayId,
            bool managerOverride) =>
            _dispatchService.AssignDiagnosesAsync(
                diagnoses,
                technicianId,
                assistantTechnicianId,
                teamId,
                supportingTeamIds,
                supportingAssignments,
                bayId,
                managerOverride);

        public Task StartDiagnosesAsync(IEnumerable<Diagnosis> diagnoses, long technicianId, long? bayId) =>
            _dispatchService.StartDiagnosesAsync(diagnoses, technicianId, bayId);

        public Task MarkSupportRequestedAsync(
            IEnumerable<Diagnosis> diagnoses,
            long requestedByStaffId,
            string? note,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto>? supportingAssignments = null) =>
            _dispatchService.MarkSupportRequestedAsync(diagnoses, requestedByStaffId, note, supportingAssignments);

        public Task AssignSupportingTeamSelectionAsync(
            IEnumerable<Diagnosis> diagnoses,
            long teamId,
            long teamLeaderId,
            long technicianId,
            long? assistantTechnicianId,
            string? note) =>
            _dispatchService.AssignSupportingTeamSelectionAsync(diagnoses, teamId, teamLeaderId, technicianId, assistantTechnicianId, note);

        public Task<IReadOnlyList<DiagnosisDispatchBoardItemDto>> GetDispatchBoardAsync(
            long? branchId,
            long? teamId,
            bool includeCompleted,
            string? viewMode = null) =>
            _dispatchService.GetDispatchBoardAsync(branchId, teamId, includeCompleted, viewMode);

        public Task<DiagnosisDispatchSuggestionDto> BuildDispatchSuggestionAsync(long serviceOrderId, long teamId) =>
            _routingService.BuildDispatchSuggestionAsync(serviceOrderId, teamId);

        // --- Intake & Handover ---

        public Task<IReadOnlyList<DiagnosisIntakePackageOptionDto>> GetIntakePackagesAsync() =>
            _intakeHandoverService.GetIntakePackagesAsync();

        public Task<DiagnosisIntakeSuggestionDto> BuildIntakeSuggestionAsync(
            long serviceOrderId,
            string? dispatchTag,
            int? bufferMinutesOverride = null,
            string? requestedPackageCode = null) =>
            _intakeHandoverService.BuildIntakeSuggestionAsync(serviceOrderId, dispatchTag, bufferMinutesOverride, requestedPackageCode);

        public Task RecordIntakeSnapshotAsync(
            ServiceOrder serviceOrder,
            DiagnosisIntakeSuggestionDto intakeSuggestion,
            string? dispatchTag,
            bool bypassConflictGuard,
            bool autoAssign,
            bool customerConsentAccepted,
            string? customerConsentChannel,
            string? customerConsentNote,
            IReadOnlyList<Diagnosis> diagnoses)
        {
            var (assignedTeamId, leadTechnicianId, assistantTechnicianId) = ExtractNewestDiagnosisAssignment(_store, diagnoses);

            return _intakeHandoverService.RecordIntakeSnapshotAsync(
                serviceOrder,
                intakeSuggestion,
                dispatchTag,
                bypassConflictGuard,
                autoAssign,
                customerConsentAccepted,
                customerConsentChannel,
                customerConsentNote,
                assignedTeamId,
                leadTechnicianId,
                assistantTechnicianId);
        }

        public Task<DiagnosisHandoverSnapshotResponseDto> CreateHandoverSnapshotAsync(long serviceOrderId, DiagnosisHandoverSnapshotCreateDto request) =>
            _intakeHandoverService.CreateHandoverSnapshotAsync(serviceOrderId, request);

        public Task<DiagnosisHandoverSnapshotResponseDto?> GetLatestHandoverSnapshotAsync(long serviceOrderId) =>
            _intakeHandoverService.GetLatestHandoverSnapshotAsync(serviceOrderId);

        public Task<DiagnosisHandoverSnapshotResponseDto> ApplyHandoverCustomerDecisionAsync(
            long serviceOrderId,
            long snapshotId,
            DiagnosisHandoverDecisionCreateDto request) =>
            _intakeHandoverService.ApplyHandoverCustomerDecisionAsync(serviceOrderId, snapshotId, request);

        public Task<DiagnosisIntakeKpiResponseDto> GetIntakeConversionKpiAsync(long? branchId, DateTime? startDate, DateTime? endDate) =>
            _intakeHandoverService.GetIntakeConversionKpiAsync(branchId, startDate, endDate);

        // --- Queries ---

        public async Task<IReadOnlyList<Diagnosis>> GetAllFilteredAsync(long? serviceOrderId, bool requestOnly, long? teamId = null)
        {
            var rows = (await _store.GetFilteredDiagnosesNoTrackingAsync(serviceOrderId, requestOnly)).ToList();
            await ApplyLatestLogOverlayAsync(_store, rows);

            if (!teamId.HasValue)
            {
                return rows;
            }

            var teamProfile = await _store.GetTeamProfileAsync(teamId.Value);
            return rows
                .Where(x => MatchesTeamFilter(
                    _store,
                    x,
                    teamId.Value,
                    teamProfile?.SpecialtyTag,
                    teamProfile?.Code,
                    teamProfile != null))
                .ToList();
        }

        public Task<ServiceOrder?> GetServiceOrderAsync(long serviceOrderId) =>
            _store.GetServiceOrderByIdAsync(serviceOrderId);

        public Task<bool> HasSignedCheckInAsync(long serviceOrderId) =>
            _store.HasSignedCheckInAsync(serviceOrderId, LogTypeCheckIn);

        public async Task<IReadOnlyList<Diagnosis>> GetAllByServiceOrderAsync(long serviceOrderId)
        {
            var rows = (await _store.GetDiagnosesByServiceOrderAsync(serviceOrderId)).ToList();
            await ApplyLatestLogOverlayAsync(_store, rows);
            return rows;
        }

        public async Task<Diagnosis?> GetDiagnosisEntityAsync(long id)
        {
            var diagnosis = await _store.GetDiagnosisByIdAsync(id);
            if (diagnosis != null)
            {
                await ApplyLatestLogOverlayAsync(_store, new[] { diagnosis });
            }
            return diagnosis;
        }

        public async Task<IReadOnlyDictionary<long, List<DiagnosisAssignment>>> GetAssignmentHistoryByDiagnosisIdsAsync(IEnumerable<long> diagnosisIds)
        {
            var normalizedIds = (diagnosisIds ?? Array.Empty<long>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (normalizedIds.Count == 0)
            {
                return new Dictionary<long, List<DiagnosisAssignment>>();
            }

            var rows = await _store.GetDiagnosisAssignmentsByDiagnosisIdsAsync(normalizedIds);

            return rows
                .GroupBy(x => x.DiagnosisId)
                .ToDictionary(g => g.Key, g => BuildVersionSnapshotsFromLogs(g.ToList()).OrderByDescending(x => x.Id).ToList());
        }

        public async Task<IReadOnlyList<Diagnosis>> GetLatestWorkflowDiagnosesByServiceOrderAsync(long serviceOrderId)
        {
            var rows = (await _store.GetDiagnosesByServiceOrderAsync(serviceOrderId)).ToList();
            await ApplyLatestLogOverlayAsync(_store, rows);
            if (rows.Count == 0)
            {
                return rows;
            }

            var candidates = rows
                .Where(x => !string.Equals(x.Status, DiagnosisStatusCanceled, StringComparison.OrdinalIgnoreCase))
                .Where(x => !IsSupersededRequeued(_store, x))
                .ToList();

            var unresolved = candidates
                .Where(x => !IsFinalApprovalDecided(GetFinalApprovalStatus(_store, x)))
                .ToList();

            return unresolved.Count > 0 ? unresolved : candidates;
        }

        public async Task RemoveRecommendationsByDiagnosisIdsAsync(IEnumerable<long> diagnosisIds)
        {
            var idList = diagnosisIds?.Distinct().ToList() ?? new List<long>();
            if (idList.Count == 0) return;

            var assignmentIds = await _store.GetAssignmentIdsByDiagnosisIdsAsync(idList);
            var recs = await _store.GetRecommendationsForRemovalAsync(idList, assignmentIds);
            if (recs.Count == 0) return;

            _store.RemoveDiagnosisRecommendations(recs);
        }

        public Task RemoveDiagnosesAsync(IEnumerable<Diagnosis> diagnoses)
        {
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count > 0)
            {
                _store.RemoveDiagnoses(list);
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
                ApplySupersededArchiveState(_store, diagnosis, now, tag, actorStaffId);
            }

            _store.UpdateDiagnoses(list);
            AppendDiagnosisAssignments(_store, list.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventArchived,
                    oldStatus: DiagnosisStatusAssigned,
                    createdBy: actorStaffId,
                    note: tag)));
            await _store.SaveChangesAsync();
        }

        // --- Workflow ---

        public Task<long> ResolvePlaceholderTechnicianIdAsync(ServiceOrder serviceOrder, string? dispatchTag = null) =>
            DiagnosisAssignmentStateHelpers.ResolvePlaceholderTechnicianIdAsync(_store, serviceOrder, dispatchTag);

        public async Task<IReadOnlyList<Diagnosis>> CreatePendingDiagnosesAsync(
            long serviceOrderId,
            long? technicianId,
            IReadOnlyList<string> symptoms,
            string? dispatchTag = null,
            long? assignedTeamId = null,
            long? assignedBayId = null,
            string? assignmentNote = null)
        {
            var now = VietnamTime.Now;
            var normalizedDispatchTag = NormalizeDispatchTag(dispatchTag);
            var normalizedAssignmentNote = string.IsNullOrWhiteSpace(assignmentNote) ? null : assignmentNote.Trim();
            var normalizedSymptoms = NormalizeQueueSymptoms(symptoms);

            var items = normalizedSymptoms
                .Select(symptom => new Diagnosis
                {
                    ServiceOrderId = serviceOrderId,
                    Symptom = string.IsNullOrWhiteSpace(symptom) ? null : symptom,
                    Status = DiagnosisStatusPending,
                    DispatchTag = normalizedDispatchTag,
                    AssignmentNote = normalizedAssignmentNote,
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();

            foreach (var diagnosis in items)
            {
                ApplyInitialAssignmentState(_store, diagnosis, technicianId, assignedTeamId, assignedBayId, now);
            }

            _store.AddDiagnoses(items);
            await _store.SaveChangesInTransactionAsync();

            // Rebind assignment state after IDs are generated to avoid losing auto-assigned team/bay
            // when building the first DiagnosisAssignment log.
            foreach (var diagnosis in items)
            {
                ApplyInitialAssignmentState(_store, diagnosis, technicianId, assignedTeamId, assignedBayId, now);
            }

            AppendDiagnosisAssignments(_store, items.Select(diagnosis =>
                BuildDiagnosisAssignment(
                    _store,
                    diagnosis,
                    DiagnoseLogEventCreated,
                    oldStatus: null,
                    createdBy: diagnosis.UpdatedBy,
                    note: "Created diagnosis ticket")));
            await _store.SaveChangesInTransactionAsync();
            return items;
        }

        public async Task UpdateServiceOrderStatusAsync(ServiceOrder serviceOrder, string status)
        {
            serviceOrder.Status = status;
            serviceOrder.UpdatedAt = VietnamTime.Now;
            _store.UpdateServiceOrder(serviceOrder);
            await _store.SaveChangesInTransactionAsync();
        }

        public async Task<IReadOnlyList<Diagnosis>> GetActiveDiagnosesAsync(long serviceOrderId)
        {
            var rows = (await _store.GetActiveDiagnosesByServiceOrderAsync(serviceOrderId)).ToList();
            await ApplyLatestLogOverlayAsync(_store, rows);
            return rows;
        }

        public Task<bool> TechnicianExistsAsync(long technicianId) =>
            _store.StaffExistsAsync(technicianId);

        public Task<Team?> GetTeamByIdAsync(long teamId) =>
            _store.GetTeamByIdAsync(teamId);

        public Task<Staff?> GetStaffByIdAsync(long staffId) =>
            _store.GetStaffByIdAsync(staffId);

        public Task<long?> ResolveTeamIdForTechnicianAsync(long technicianId) =>
            _store.ResolveTeamIdForTechnicianAsync(technicianId);

        public Task<bool> IsTechnicianInTeamAsync(long technicianId, long teamId) =>
            _store.IsTechnicianInTeamAsync(technicianId, teamId);
    }
}
