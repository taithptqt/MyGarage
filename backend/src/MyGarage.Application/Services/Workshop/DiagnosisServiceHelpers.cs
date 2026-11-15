using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Enums;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisAssignmentStateHelpers;
using static MyGarage.Services.DiagnosisDispatchCollaborationHelpers;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    internal static class DiagnosisServiceHelpers
    {
        internal static int CalculateTeamLeaderApprovalPendingMinutes(Diagnosis diagnosis, DateTime? now)
        {
            var reference = diagnosis.SubmittedAt ?? diagnosis.CreatedAt ?? diagnosis.UpdatedAt;
            if (!reference.HasValue)
            {
                return 0;
            }

            var current = now ?? VietnamTime.Now;
            var elapsed = (int)Math.Floor((current - reference.Value).TotalMinutes);
            return elapsed > 0 ? elapsed : 0;
        }

        internal static bool IsTeamLeaderApprovalPending(IDiagnosisStore store, Diagnosis diagnosis)
        {
            var finalStatus = (GetFinalApprovalStatus(store, diagnosis) ?? string.Empty).Trim();
            if (IsFinalApprovalDecided(finalStatus))
            {
                return false;
            }

            var teamLeaderStatus = (GetTeamLeaderApprovalStatus(store, diagnosis) ?? string.Empty).Trim();
            return !string.Equals(teamLeaderStatus, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsFinalApprovalDecided(string? finalStatus) =>
            string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.REJECTED), StringComparison.OrdinalIgnoreCase);

        internal static bool MatchesTeamFilter(
            IDiagnosisStore store,
            Diagnosis diagnosis,
            long teamId,
            string? teamSpecialtyTag,
            string? teamCode,
            bool hasTeamProfile)
        {
            var assignedTeamId = GetAssignedTeamId(store, diagnosis);
            if (assignedTeamId == teamId)
            {
                return true;
            }

            if (TryParseDispatchCollaborationNote(null, GetForemanNote(store, diagnosis))?.TeamIds.Contains(teamId) ?? false)
            {
                return true;
            }

            if (assignedTeamId.HasValue || !IsPendingLikeStatus(diagnosis.Status))
            {
                return false;
            }

            return !hasTeamProfile ||
                   string.IsNullOrWhiteSpace(diagnosis.DispatchTag) ||
                   SplitDispatchTags(diagnosis.DispatchTag).Any(tag => DispatchTagMatchesTeam(tag, teamSpecialtyTag, teamCode));
        }

        internal static bool IsSupersededRequeued(IDiagnosisStore store, Diagnosis diagnosis)
        {
            var supportNote = GetSupportRequestNote(store, diagnosis) ?? string.Empty;
            return supportNote.Contains("[SUPERSEDED_REQUEUED]", StringComparison.OrdinalIgnoreCase);
        }

        internal static void ApplySupersededArchiveState(
            IDiagnosisStore store,
            Diagnosis diagnosis,
            DateTime now,
            string tag,
            long? actorStaffId)
        {
            diagnosis.Status = DiagnosisStatusCompleted;
            diagnosis.DiagnosedAt ??= now;
            SetCancellationStatus(store, diagnosis, nameof(DiagnosisCancellationStatusCode.NONE));
            SetCancellationRequestedAt(store, diagnosis, null);
            SetCancellationRequestedBy(store, diagnosis, null);
            SetCancellationDecisionAt(store, diagnosis, null);
            SetCancellationDecisionBy(store, diagnosis, null);
            SetCancellationDecisionNote(store, diagnosis, null);

            var supportNote = GetSupportRequestNote(store, diagnosis);
            SetSupportRequestNote(store, diagnosis, string.IsNullOrWhiteSpace(supportNote)
                ? tag
                : supportNote!.Contains(tag, StringComparison.OrdinalIgnoreCase)
                    ? supportNote
                    : $"{supportNote.Trim()} {tag}");

            diagnosis.UpdatedAt = now;
            diagnosis.UpdatedBy = actorStaffId;
        }

        internal static List<string> NormalizeQueueSymptoms(IReadOnlyList<string>? symptoms)
        {
            var normalized = (symptoms ?? Array.Empty<string>())
                .Select(symptom => (symptom ?? string.Empty).Trim())
                .Where(IsMeaningfulQueueSymptom)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalized.Count == 0)
            {
                normalized.Add(string.Empty);
            }

            return normalized;
        }

        internal static bool IsMeaningfulQueueSymptom(string? symptom)
        {
            var value = (symptom ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.ToLowerInvariant();
            if (normalized.StartsWith("[flow:", StringComparison.Ordinal) ||
                normalized.StartsWith("[cluster:", StringComparison.Ordinal) ||
                normalized.StartsWith("[dispatch", StringComparison.Ordinal) ||
                normalized.StartsWith("[auto_dispatch", StringComparison.Ordinal))
            {
                return false;
            }

            if (normalized == "khong co" ||
                normalized == "khong co ghi chu" ||
                normalized == "khong co ghi chu gi" ||
                normalized == "khong co mo ta")
            {
                return false;
            }

            if (normalized.StartsWith("[tiep nhan]", StringComparison.Ordinal) &&
                normalized.Contains("khong co", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        internal static void ApplyInitialAssignmentState(
            IDiagnosisStore store,
            Diagnosis diagnosis,
            long? technicianId,
            long? assignedTeamId,
            long? assignedBayId,
            DateTime now)
        {
            SetTechnicianId(store, diagnosis, technicianId.HasValue && technicianId.Value > 0 ? technicianId : null);
            SetAssistantTechnicianId(store, diagnosis, null);
            SetAssignedBayId(store, diagnosis, assignedBayId.HasValue && assignedBayId.Value > 0 ? assignedBayId : null);
            SetAssignedTeamId(store, diagnosis, assignedTeamId.HasValue && assignedTeamId.Value > 0 ? assignedTeamId : null);
            SetAssignedAt(store, diagnosis, (assignedTeamId.HasValue && assignedTeamId.Value > 0) || (assignedBayId.HasValue && assignedBayId.Value > 0) ? now : null);
            SetTechnicianAcceptedAt(store, diagnosis, null);
            SetTechnicianCompletedAt(store, diagnosis, null);
            SetSupportRequestedAt(store, diagnosis, null);
            SetSupportRequestedBy(store, diagnosis, null);
            SetSupportRequestNote(store, diagnosis, null);
            SetTeamLeaderApprovalStatus(store, diagnosis, nameof(ApprovalDecisionStatusCode.PENDING));
            SetFinalApprovalStatus(store, diagnosis, nameof(ApprovalDecisionStatusCode.PENDING));
            SetForemanRejectReason(store, diagnosis, null);
            SetApprovedByForemanId(store, diagnosis, null);
            SetApprovedByForemanAt(store, diagnosis, null);
            SetApprovedByTeamLeaderId(store, diagnosis, null);
            SetApprovedByTeamLeaderAt(store, diagnosis, null);
            SetTeamLeaderNote(store, diagnosis, null);
            SetForemanNote(store, diagnosis, null);
            SetCancellationStatus(store, diagnosis, CancellationStatusNone);
            SetCancellationReason(store, diagnosis, null);
            SetCancellationRequestedAt(store, diagnosis, null);
            SetCancellationRequestedBy(store, diagnosis, null);
            SetCancellationDecisionAt(store, diagnosis, null);
            SetCancellationDecisionBy(store, diagnosis, null);
            SetCancellationDecisionNote(store, diagnosis, null);
        }

        internal static (long? AssignedTeamId, long? LeadTechnicianId, long? AssistantTechnicianId) ExtractNewestDiagnosisAssignment(
            IDiagnosisStore store,
            IReadOnlyList<Diagnosis>? diagnoses)
        {
            var newest = (diagnoses ?? Array.Empty<Diagnosis>())
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                .FirstOrDefault();

            if (newest == null)
            {
                return (null, null, null);
            }

            return (
                GetAssignedTeamId(store, newest),
                GetTechnicianId(store, newest),
                GetAssistantTechnicianId(store, newest));
        }
    }
}
