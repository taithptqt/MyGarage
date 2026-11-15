using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Enums;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    internal static class DiagnosisAssignmentStateHelpers
    {
        internal const string DiagnosisStatusCompleted = nameof(DiagnosisStatus.COMPLETED);
        internal const string DiagnosisStatusCanceled = nameof(DiagnosisStatus.CANCELED);

        internal const string CancellationStatusNone = nameof(DiagnosisCancellationStatusCode.NONE);
        internal const string CancellationStatusRequested = nameof(DiagnosisCancellationStatusCode.REQUESTED);
        internal const string CancellationStatusApproved = nameof(DiagnosisCancellationStatusCode.APPROVED);
        internal const string CancellationStatusRejected = nameof(DiagnosisCancellationStatusCode.REJECTED);

        internal const string ServiceOrderStatusCheckedIn = nameof(ServiceOrderLifecycleStatus.CHECKED_IN);

        internal const int UnclaimedEscalationMinutes = 10;
        internal const int TeamLeaderApprovalWarningMinutesDefault = 15;
        internal const int TeamLeaderApprovalEscalationMinutesDefault = 30;
        internal const int GoldenCancelWindowMinutes = 10;
        internal const int DiagnosisSlaMinutes = 45;

        internal const string DiagnoseLogEventCreated = "CREATED";
        internal const string DiagnoseLogEventAssigned = "ASSIGNED";
        internal const string DiagnoseLogEventStarted = "STARTED";
        internal const string DiagnoseLogEventSupportRequested = "SUPPORT_REQUESTED";
        internal const string DiagnoseLogEventSupportSelection = "SUPPORT_SELECTION";
        internal const string DiagnoseLogEventCancellationRequested = "CANCELLATION_REQUESTED";
        internal const string DiagnoseLogEventCancellationApproved = "CANCELLATION_APPROVED";
        internal const string DiagnoseLogEventCancellationRejected = "CANCELLATION_REJECTED";
        internal const string DiagnoseLogEventCanceled = "CANCELED";
        internal const string DiagnoseLogEventCompleted = "COMPLETED";
        internal const string DiagnoseLogEventArchived = "ARCHIVED";
        internal const string DiagnoseLogEventTechnicalApproved = "TECHNICAL_APPROVED";
        internal const string DiagnoseLogEventTechnicalRejected = "TECHNICAL_REJECTED";
        internal const string DiagnoseLogEventFinalApproved = "FINAL_APPROVED";
        internal const string DiagnoseLogEventFinalRejected = "FINAL_REJECTED";
        internal const string DiagnoseLogEventFinalApprovedBypass = "FINAL_APPROVED_BYPASS";
        internal const string DiagnoseLogEventRediagnoseRequested = "REDIAGNOSE_REQUESTED";

        private static readonly ConditionalWeakTable<Diagnosis, DiagnosisAssignment> StateByDiagnosis = new();

        private static readonly HashSet<string> VersionAnchorEvents = new(StringComparer.OrdinalIgnoreCase)
        {
            DiagnoseLogEventCreated,
            DiagnoseLogEventRediagnoseRequested,
        };

        internal static DiagnosisAssignment CloneAssignmentState(DiagnosisAssignment source)
        {
            return new DiagnosisAssignment
            {
                Id = source.Id,
                DiagnosisId = source.DiagnosisId,
                AssignedTeamId = source.AssignedTeamId,
                TechnicianId = source.TechnicianId,
                AssistantTechnicianId = source.AssistantTechnicianId,
                AssignedBayId = source.AssignedBayId,
                Status = source.Status,
                AssignedAt = source.AssignedAt,
                TechnicianAcceptedAt = source.TechnicianAcceptedAt,
                TechnicianCompletedAt = source.TechnicianCompletedAt,
                CancellationStatus = source.CancellationStatus,
                CancellationReason = source.CancellationReason,
                CancellationRequestedBy = source.CancellationRequestedBy,
                CancellationRequestedAt = source.CancellationRequestedAt,
                CancellationDecisionBy = source.CancellationDecisionBy,
                CancellationDecisionAt = source.CancellationDecisionAt,
                CancellationDecisionNote = source.CancellationDecisionNote,
                SupportRequestedBy = source.SupportRequestedBy,
                SupportRequestedAt = source.SupportRequestedAt,
                SupportRequestNote = source.SupportRequestNote,
                TeamLeaderApprovalStatus = source.TeamLeaderApprovalStatus,
                FinalApprovalStatus = source.FinalApprovalStatus,
                ForemanRejectReason = source.ForemanRejectReason,
                ApprovedByForemanId = source.ApprovedByForemanId,
                ApprovedByForemanAt = source.ApprovedByForemanAt,
                ApprovedByTeamLeaderId = source.ApprovedByTeamLeaderId,
                ApprovedByTeamLeaderAt = source.ApprovedByTeamLeaderAt,
                TeamLeaderNote = source.TeamLeaderNote,
                ForemanNote = source.ForemanNote,
                RequiresInsuranceProof = source.RequiresInsuranceProof,
                ProofImageUrl = source.ProofImageUrl,
                EventType = source.EventType,
                OldStatus = source.OldStatus,
                Note = source.Note,
                CreatedBy = source.CreatedBy,
                CreatedAt = source.CreatedAt
            };
        }

        internal static DiagnosisAssignment EnsureAssignmentState(IDiagnosisStore store, Diagnosis diagnosis)
        {
            if (StateByDiagnosis.TryGetValue(diagnosis, out var cached))
            {
                return cached;
            }

            var historyLogs = diagnosis.Id > 0
                ? store.GetDiagnosisAssignmentsForDiagnosisAsync(diagnosis.Id)
                    .GetAwaiter()
                    .GetResult()
                    .OrderBy(x => x.Id)
                    .ThenBy(x => x.CreatedAt)
                    .ToList()
                : new List<DiagnosisAssignment>();

            DiagnosisAssignment? latest = null;
            if (historyLogs.Count > 0)
            {
                latest = historyLogs[^1];
            }
            else if (diagnosis.DiagnosisAssignments != null && diagnosis.DiagnosisAssignments.Count > 0)
            {
                latest = diagnosis.DiagnosisAssignments
                    .OrderByDescending(x => x.Id)
                    .ThenByDescending(x => x.CreatedAt)
                    .FirstOrDefault();
            }

            var state = latest != null
                ? CloneAssignmentState(latest)
                : new DiagnosisAssignment
                {
                    DiagnosisId = diagnosis.Id,
                    Status = diagnosis.Status,
                    EventType = DiagnoseLogEventCreated,
                    CreatedAt = diagnosis.UpdatedAt ?? diagnosis.CreatedAt ?? VietnamTime.Now
                };

            var versionSnapshots = BuildVersionSnapshotsFromLogs(historyLogs);
            if (versionSnapshots.Count > 0)
            {
                state.Id = versionSnapshots[^1].Id;
            }

            StateByDiagnosis.AddOrUpdate(diagnosis, state);
            return state;
        }

        internal static async Task PreloadAssignmentStatesAsync(IDiagnosisStore store, IReadOnlyCollection<Diagnosis> diagnoses)
        {
            if (diagnoses == null || diagnoses.Count == 0)
            {
                return;
            }

            var diagnosisIds = diagnoses.Select(x => x.Id).Distinct().ToList();
            var allLogs = await store.GetDiagnosisAssignmentsByDiagnosisIdsAsync(diagnosisIds);

            var groupedByDiagnosis = allLogs
                .GroupBy(x => x.DiagnosisId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var diagnosis in diagnoses)
            {
                if (!groupedByDiagnosis.TryGetValue(diagnosis.Id, out var logs) || logs.Count == 0)
                {
                    continue;
                }

                var latest = logs[^1];
                var clone = CloneAssignmentState(latest);
                var versionSnapshots = BuildVersionSnapshotsFromLogs(logs);
                if (versionSnapshots.Count > 0)
                {
                    clone.Id = versionSnapshots[^1].Id;
                }
                StateByDiagnosis.AddOrUpdate(diagnosis, clone);
                diagnosis.DiagnosisAssignments = new List<DiagnosisAssignment> { clone };
            }
        }

        internal static Task ApplyLatestLogOverlayAsync(IDiagnosisStore store, IReadOnlyList<Diagnosis> diagnoses)
        {
            return PreloadAssignmentStatesAsync(store, diagnoses);
        }

        private static bool IsAssignmentIdentityChanged(DiagnosisAssignment current, DiagnosisAssignment? previous)
        {
            if (previous == null)
            {
                return true;
            }

            return current.AssignedTeamId != previous.AssignedTeamId
                || current.TechnicianId != previous.TechnicianId
                || current.AssistantTechnicianId != previous.AssistantTechnicianId;
        }

        private static bool IsVersionAnchor(DiagnosisAssignment current, DiagnosisAssignment? previous)
        {
            var eventType = (current.EventType ?? string.Empty).Trim();
            if (VersionAnchorEvents.Contains(eventType))
            {
                return true;
            }

            if (string.Equals(eventType, DiagnoseLogEventAssigned, StringComparison.OrdinalIgnoreCase))
            {
                return IsAssignmentIdentityChanged(current, previous);
            }

            return false;
        }

        private static DiagnosisAssignment BuildVersionSnapshot(DiagnosisAssignment anchor, DiagnosisAssignment latestInBucket)
        {
            var snapshot = CloneAssignmentState(latestInBucket);
            snapshot.Id = anchor.Id;
            snapshot.CreatedAt = anchor.CreatedAt;
            snapshot.EventType = anchor.EventType;
            snapshot.Note = anchor.Note;
            snapshot.OldStatus = anchor.OldStatus;
            return snapshot;
        }

        internal static IReadOnlyList<DiagnosisAssignment> BuildVersionSnapshotsFromLogs(IReadOnlyList<DiagnosisAssignment> logs)
        {
            if (logs == null || logs.Count == 0)
            {
                return Array.Empty<DiagnosisAssignment>();
            }

            var ordered = logs
                .Where(x => x != null)
                .OrderBy(x => x.Id)
                .ThenBy(x => x.CreatedAt)
                .ToList();
            if (ordered.Count == 0)
            {
                return Array.Empty<DiagnosisAssignment>();
            }

            var snapshots = new List<DiagnosisAssignment>();
            DiagnosisAssignment? previous = null;
            DiagnosisAssignment? currentAnchor = null;
            DiagnosisAssignment? latestInBucket = null;

            foreach (var log in ordered)
            {
                if (IsVersionAnchor(log, previous))
                {
                    if (currentAnchor != null && latestInBucket != null)
                    {
                        snapshots.Add(BuildVersionSnapshot(currentAnchor, latestInBucket));
                    }

                    currentAnchor = log;
                    latestInBucket = log;
                }
                else
                {
                    latestInBucket = log;
                }

                previous = log;
            }

            if (currentAnchor != null && latestInBucket != null)
            {
                snapshots.Add(BuildVersionSnapshot(currentAnchor, latestInBucket));
            }

            if (snapshots.Count == 0)
            {
                snapshots.Add(BuildVersionSnapshot(ordered[0], ordered[^1]));
            }

            return snapshots;
        }

        internal static DiagnosisAssignment BuildDiagnosisAssignment(
            IDiagnosisStore store,
            Diagnosis diagnosis,
            string eventType,
            string? oldStatus = null,
            long? createdBy = null,
            string? note = null,
            string? cancelReason = null)
        {
            var createdAt = diagnosis.UpdatedAt ?? diagnosis.CreatedAt ?? VietnamTime.Now;
            var state = EnsureAssignmentState(store, diagnosis);
            return new DiagnosisAssignment
            {
                DiagnosisId = diagnosis.Id,
                AssignedTeamId = state.AssignedTeamId,
                TechnicianId = state.TechnicianId,
                AssistantTechnicianId = state.AssistantTechnicianId,
                AssignedBayId = state.AssignedBayId,
                Status = diagnosis.Status,
                AssignedAt = state.AssignedAt,
                TechnicianAcceptedAt = state.TechnicianAcceptedAt,
                TechnicianCompletedAt = state.TechnicianCompletedAt,
                CancellationStatus = state.CancellationStatus,
                CancellationReason = string.IsNullOrWhiteSpace(cancelReason) ? state.CancellationReason : cancelReason,
                CancellationRequestedBy = state.CancellationRequestedBy,
                CancellationRequestedAt = state.CancellationRequestedAt,
                CancellationDecisionBy = state.CancellationDecisionBy,
                CancellationDecisionAt = state.CancellationDecisionAt,
                CancellationDecisionNote = state.CancellationDecisionNote,
                SupportRequestedBy = state.SupportRequestedBy,
                SupportRequestedAt = state.SupportRequestedAt,
                SupportRequestNote = state.SupportRequestNote,
                TeamLeaderApprovalStatus = state.TeamLeaderApprovalStatus,
                FinalApprovalStatus = state.FinalApprovalStatus,
                ForemanRejectReason = state.ForemanRejectReason,
                ApprovedByForemanId = state.ApprovedByForemanId,
                ApprovedByForemanAt = state.ApprovedByForemanAt,
                ApprovedByTeamLeaderId = state.ApprovedByTeamLeaderId,
                ApprovedByTeamLeaderAt = state.ApprovedByTeamLeaderAt,
                TeamLeaderNote = state.TeamLeaderNote,
                ForemanNote = state.ForemanNote,
                EventType = eventType,
                OldStatus = oldStatus,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                CreatedBy = createdBy,
                CreatedAt = createdAt
            };
        }

        internal static void AppendDiagnosisAssignments(IDiagnosisStore store, IEnumerable<DiagnosisAssignment> logs)
        {
            var items = logs?.Where(x => x != null).ToList() ?? new List<DiagnosisAssignment>();
            if (items.Count == 0)
            {
                return;
            }

            store.AddDiagnosisAssignments(items);
        }

        internal static long? GetAssignedTeamId(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).AssignedTeamId;
        internal static long? GetTechnicianId(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).TechnicianId;
        internal static long? GetAssistantTechnicianId(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).AssistantTechnicianId;
        internal static long? GetAssignedBayId(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).AssignedBayId;
        internal static DateTime? GetAssignedAt(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).AssignedAt;
        internal static DateTime? GetTechnicianAcceptedAt(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).TechnicianAcceptedAt;
        internal static DateTime? GetTechnicianCompletedAt(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).TechnicianCompletedAt;
        internal static string? GetCancellationStatus(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).CancellationStatus;
        internal static string? GetCancellationReason(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).CancellationReason;
        internal static long? GetCancellationRequestedBy(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).CancellationRequestedBy;
        internal static DateTime? GetCancellationRequestedAt(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).CancellationRequestedAt;
        internal static long? GetCancellationDecisionBy(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).CancellationDecisionBy;
        internal static DateTime? GetCancellationDecisionAt(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).CancellationDecisionAt;
        internal static string? GetCancellationDecisionNote(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).CancellationDecisionNote;
        internal static long? GetSupportRequestedBy(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).SupportRequestedBy;
        internal static DateTime? GetSupportRequestedAt(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).SupportRequestedAt;
        internal static string? GetSupportRequestNote(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).SupportRequestNote;
        internal static string? GetTeamLeaderApprovalStatus(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).TeamLeaderApprovalStatus;
        internal static string? GetFinalApprovalStatus(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).FinalApprovalStatus;
        internal static string? GetForemanRejectReason(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).ForemanRejectReason;
        internal static long? GetApprovedByForemanId(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).ApprovedByForemanId;
        internal static DateTime? GetApprovedByForemanAt(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).ApprovedByForemanAt;
        internal static long? GetApprovedByTeamLeaderId(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).ApprovedByTeamLeaderId;
        internal static DateTime? GetApprovedByTeamLeaderAt(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).ApprovedByTeamLeaderAt;
        internal static string? GetTeamLeaderNote(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).TeamLeaderNote;
        internal static string? GetForemanNote(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).ForemanNote;
        internal static string? GetAssignmentStatus(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).Status;
        internal static string? GetAssignmentEventType(IDiagnosisStore store, Diagnosis diagnosis) => EnsureAssignmentState(store, diagnosis).EventType;

        internal static void SetAssignedTeamId(IDiagnosisStore store, Diagnosis diagnosis, long? value) => EnsureAssignmentState(store, diagnosis).AssignedTeamId = value;
        internal static void SetTechnicianId(IDiagnosisStore store, Diagnosis diagnosis, long? value) => EnsureAssignmentState(store, diagnosis).TechnicianId = value;
        internal static void SetAssistantTechnicianId(IDiagnosisStore store, Diagnosis diagnosis, long? value) => EnsureAssignmentState(store, diagnosis).AssistantTechnicianId = value;
        internal static void SetAssignedBayId(IDiagnosisStore store, Diagnosis diagnosis, long? value) => EnsureAssignmentState(store, diagnosis).AssignedBayId = value;
        internal static void SetAssignedAt(IDiagnosisStore store, Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(store, diagnosis).AssignedAt = value;
        internal static void SetTechnicianAcceptedAt(IDiagnosisStore store, Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(store, diagnosis).TechnicianAcceptedAt = value;
        internal static void SetTechnicianCompletedAt(IDiagnosisStore store, Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(store, diagnosis).TechnicianCompletedAt = value;
        internal static void SetCancellationStatus(IDiagnosisStore store, Diagnosis diagnosis, string? value) => EnsureAssignmentState(store, diagnosis).CancellationStatus = value;
        internal static void SetCancellationReason(IDiagnosisStore store, Diagnosis diagnosis, string? value) => EnsureAssignmentState(store, diagnosis).CancellationReason = value;
        internal static void SetCancellationRequestedBy(IDiagnosisStore store, Diagnosis diagnosis, long? value) => EnsureAssignmentState(store, diagnosis).CancellationRequestedBy = value;
        internal static void SetCancellationRequestedAt(IDiagnosisStore store, Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(store, diagnosis).CancellationRequestedAt = value;
        internal static void SetCancellationDecisionBy(IDiagnosisStore store, Diagnosis diagnosis, long? value) => EnsureAssignmentState(store, diagnosis).CancellationDecisionBy = value;
        internal static void SetCancellationDecisionAt(IDiagnosisStore store, Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(store, diagnosis).CancellationDecisionAt = value;
        internal static void SetCancellationDecisionNote(IDiagnosisStore store, Diagnosis diagnosis, string? value) => EnsureAssignmentState(store, diagnosis).CancellationDecisionNote = value;
        internal static void SetSupportRequestedBy(IDiagnosisStore store, Diagnosis diagnosis, long? value) => EnsureAssignmentState(store, diagnosis).SupportRequestedBy = value;
        internal static void SetSupportRequestedAt(IDiagnosisStore store, Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(store, diagnosis).SupportRequestedAt = value;
        internal static void SetSupportRequestNote(IDiagnosisStore store, Diagnosis diagnosis, string? value) => EnsureAssignmentState(store, diagnosis).SupportRequestNote = value;
        internal static void SetTeamLeaderApprovalStatus(IDiagnosisStore store, Diagnosis diagnosis, string? value) => EnsureAssignmentState(store, diagnosis).TeamLeaderApprovalStatus = value;
        internal static void SetFinalApprovalStatus(IDiagnosisStore store, Diagnosis diagnosis, string? value) => EnsureAssignmentState(store, diagnosis).FinalApprovalStatus = value;
        internal static void SetForemanRejectReason(IDiagnosisStore store, Diagnosis diagnosis, string? value) => EnsureAssignmentState(store, diagnosis).ForemanRejectReason = value;
        internal static void SetApprovedByForemanId(IDiagnosisStore store, Diagnosis diagnosis, long? value) => EnsureAssignmentState(store, diagnosis).ApprovedByForemanId = value;
        internal static void SetApprovedByForemanAt(IDiagnosisStore store, Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(store, diagnosis).ApprovedByForemanAt = value;
        internal static void SetApprovedByTeamLeaderId(IDiagnosisStore store, Diagnosis diagnosis, long? value) => EnsureAssignmentState(store, diagnosis).ApprovedByTeamLeaderId = value;
        internal static void SetApprovedByTeamLeaderAt(IDiagnosisStore store, Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(store, diagnosis).ApprovedByTeamLeaderAt = value;
        internal static void SetTeamLeaderNote(IDiagnosisStore store, Diagnosis diagnosis, string? value) => EnsureAssignmentState(store, diagnosis).TeamLeaderNote = value;
        internal static void SetForemanNote(IDiagnosisStore store, Diagnosis diagnosis, string? value) => EnsureAssignmentState(store, diagnosis).ForemanNote = value;

        internal static int GetTeamLeaderApprovalWarningMinutes(IDiagnosisStore store, long? executeBranchId = null)
        {
            if (executeBranchId.HasValue && executeBranchId.Value > 0)
            {
                var branchConfigured = store.GetBranchTeamLeaderWarningMinutes(executeBranchId.Value);
                if (branchConfigured.HasValue && branchConfigured.Value > 0)
                {
                    return branchConfigured.Value;
                }
            }

            var configured = store.GetDefaultTeamLeaderWarningMinutes();
            return configured.HasValue && configured.Value > 0
                ? configured.Value
                : TeamLeaderApprovalWarningMinutesDefault;
        }

        internal static int GetTeamLeaderApprovalEscalationMinutes(IDiagnosisStore store, long? executeBranchId = null)
        {
            var warning = GetTeamLeaderApprovalWarningMinutes(store, executeBranchId);
            if (executeBranchId.HasValue && executeBranchId.Value > 0)
            {
                var branchConfigured = store.GetBranchTeamLeaderEscalationMinutes(executeBranchId.Value);
                if (branchConfigured.HasValue && branchConfigured.Value > 0)
                {
                    return branchConfigured.Value <= warning ? warning + 1 : branchConfigured.Value;
                }
            }

            var configured = store.GetDefaultTeamLeaderEscalationMinutes();
            var resolved = configured.HasValue && configured.Value > 0 ? configured.Value : TeamLeaderApprovalEscalationMinutesDefault;
            return resolved <= warning ? warning + 1 : resolved;
        }

        internal static bool IsCancellationPending(IDiagnosisStore store, Diagnosis diagnosis)
        {
            return string.Equals(GetCancellationStatus(store, diagnosis), CancellationStatusRequested, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsPendingLikeStatus(string? status)
        {
            var normalized = NormalizeToken(status);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return normalized == NormalizeToken(DiagnosisStatusPending)
                || normalized == "NEW"
                || normalized == "REQUESTED"
                || normalized == "QUEUED";
        }

        internal static bool IsWithinGoldenCancelWindow(IDiagnosisStore store, Diagnosis diagnosis, DateTime now)
        {
            var assignedAt = GetAssignedAt(store, diagnosis) ?? diagnosis.UpdatedAt ?? diagnosis.CreatedAt;
            if (!assignedAt.HasValue)
            {
                return false;
            }

            var minutes = (now - assignedAt.Value).TotalMinutes;
            return minutes >= 0 && minutes <= GoldenCancelWindowMinutes;
        }

        internal static async Task<long> ResolvePlaceholderTechnicianIdAsync(IDiagnosisStore store, ServiceOrder serviceOrder, string? dispatchTag = null)
        {
            _ = dispatchTag;

            if (serviceOrder.ReceptionistId.HasValue)
            {
                var receptionistId = await TryResolveFrontOfficeStaffIdAsync(store, serviceOrder.ReceptionistId.Value);
                if (receptionistId.HasValue)
                {
                    return receptionistId.Value;
                }
            }

            if (serviceOrder.ServiceAdvisorId.HasValue)
            {
                var advisorId = await TryResolveFrontOfficeStaffIdAsync(store, serviceOrder.ServiceAdvisorId.Value);
                if (advisorId.HasValue)
                {
                    return advisorId.Value;
                }
            }

            if (serviceOrder.CreatedBy.HasValue)
            {
                var createdById = await TryResolveFrontOfficeStaffIdAsync(store, serviceOrder.CreatedBy.Value);
                if (createdById.HasValue)
                {
                    return createdById.Value;
                }
            }

            var branchFrontOfficeId = await store.GetBranchFrontOfficeStaffIdAsync(serviceOrder.ExecuteBranchId, StaffStatusActive);
            if (branchFrontOfficeId.HasValue && branchFrontOfficeId.Value > 0)
            {
                return branchFrontOfficeId.Value;
            }

            throw new InvalidOperationException("Không tìm thấy nhân sự tiếp nhận/CVDV để tạo hàng chờ chẩn đoán.");
        }

        private static async Task<long?> TryResolveFrontOfficeStaffIdAsync(IDiagnosisStore store, long staffId)
        {
            if (staffId <= 0)
            {
                return null;
            }

            var candidate = await store.GetActiveStaffTeamInfoAsync(staffId, StaffStatusActive);
            if (candidate == null)
            {
                return null;
            }

            if (candidate.Value.TeamId.HasValue && candidate.Value.TeamId.Value > 0)
            {
                return null;
            }

            return candidate.Value.Id;
        }
    }
}
