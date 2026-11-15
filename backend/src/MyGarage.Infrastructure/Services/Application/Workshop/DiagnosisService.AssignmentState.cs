using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services;

public partial class DiagnosisService
{
    private readonly Dictionary<long, DiagnosisAssignment> _assignmentStateByDiagnosisId = new();
    private static readonly HashSet<string> VersionAnchorEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        DiagnoseLogEventCreated,
        DiagnoseLogEventRediagnoseRequested,
    };

    private static DiagnosisAssignment CloneAssignmentState(DiagnosisAssignment source)
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

    private DiagnosisAssignment EnsureAssignmentState(Diagnosis diagnosis)
    {
        if (_assignmentStateByDiagnosisId.TryGetValue(diagnosis.Id, out var cached))
        {
            return cached;
        }

        var historyLogs = _context.DiagnosisAssignments.AsNoTracking()
            .Where(x => x.DiagnosisId == diagnosis.Id)
            .OrderBy(x => x.Id)
            .ThenBy(x => x.CreatedAt)
            .ToList();

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

        _assignmentStateByDiagnosisId[diagnosis.Id] = state;
        return state;
    }

    private async Task PreloadAssignmentStatesAsync(IReadOnlyCollection<Diagnosis> diagnoses)
    {
        if (diagnoses == null || diagnoses.Count == 0)
        {
            return;
        }

        var diagnosisIds = diagnoses.Select(x => x.Id).Distinct().ToList();
        var allLogs = await _context.DiagnosisAssignments.AsNoTracking()
            .Where(x => diagnosisIds.Contains(x.DiagnosisId))
            .OrderBy(x => x.DiagnosisId)
            .ThenBy(x => x.Id)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync();

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
            _assignmentStateByDiagnosisId[diagnosis.Id] = clone;
            diagnosis.DiagnosisAssignments = new List<DiagnosisAssignment> { clone };
        }
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

    private static IReadOnlyList<DiagnosisAssignment> BuildVersionSnapshotsFromLogs(IReadOnlyList<DiagnosisAssignment> logs)
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

    private long? GetAssignedTeamId(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).AssignedTeamId;
    private long? GetTechnicianId(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).TechnicianId;
    private long? GetAssistantTechnicianId(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).AssistantTechnicianId;
    private long? GetAssignedBayId(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).AssignedBayId;
    private DateTime? GetAssignedAt(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).AssignedAt;
    private DateTime? GetTechnicianAcceptedAt(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).TechnicianAcceptedAt;
    private DateTime? GetTechnicianCompletedAt(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).TechnicianCompletedAt;
    private string? GetCancellationStatus(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).CancellationStatus;
    private string? GetCancellationReason(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).CancellationReason;
    private long? GetCancellationRequestedBy(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).CancellationRequestedBy;
    private DateTime? GetCancellationRequestedAt(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).CancellationRequestedAt;
    private long? GetCancellationDecisionBy(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).CancellationDecisionBy;
    private DateTime? GetCancellationDecisionAt(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).CancellationDecisionAt;
    private string? GetCancellationDecisionNote(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).CancellationDecisionNote;
    private long? GetSupportRequestedBy(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).SupportRequestedBy;
    private DateTime? GetSupportRequestedAt(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).SupportRequestedAt;
    private string? GetSupportRequestNote(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).SupportRequestNote;
    private string? GetTeamLeaderApprovalStatus(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).TeamLeaderApprovalStatus;
    private string? GetFinalApprovalStatus(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).FinalApprovalStatus;
    private string? GetForemanRejectReason(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).ForemanRejectReason;
    private long? GetApprovedByForemanId(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).ApprovedByForemanId;
    private DateTime? GetApprovedByForemanAt(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).ApprovedByForemanAt;
    private long? GetApprovedByTeamLeaderId(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).ApprovedByTeamLeaderId;
    private DateTime? GetApprovedByTeamLeaderAt(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).ApprovedByTeamLeaderAt;
    private string? GetTeamLeaderNote(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).TeamLeaderNote;
    private string? GetForemanNote(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).ForemanNote;
    private bool GetRequiresInsuranceProof(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).RequiresInsuranceProof;
    private string? GetProofImageUrl(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).ProofImageUrl;
    private string? GetAssignmentStatus(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).Status;
    private string? GetAssignmentEventType(Diagnosis diagnosis) => EnsureAssignmentState(diagnosis).EventType;

    private void SetAssignedTeamId(Diagnosis diagnosis, long? value) => EnsureAssignmentState(diagnosis).AssignedTeamId = value;
    private void SetTechnicianId(Diagnosis diagnosis, long? value) => EnsureAssignmentState(diagnosis).TechnicianId = value;
    private void SetAssistantTechnicianId(Diagnosis diagnosis, long? value) => EnsureAssignmentState(diagnosis).AssistantTechnicianId = value;
    private void SetAssignedBayId(Diagnosis diagnosis, long? value) => EnsureAssignmentState(diagnosis).AssignedBayId = value;
    private void SetAssignedAt(Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(diagnosis).AssignedAt = value;
    private void SetTechnicianAcceptedAt(Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(diagnosis).TechnicianAcceptedAt = value;
    private void SetTechnicianCompletedAt(Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(diagnosis).TechnicianCompletedAt = value;
    private void SetCancellationStatus(Diagnosis diagnosis, string? value) => EnsureAssignmentState(diagnosis).CancellationStatus = value;
    private void SetCancellationReason(Diagnosis diagnosis, string? value) => EnsureAssignmentState(diagnosis).CancellationReason = value;
    private void SetCancellationRequestedBy(Diagnosis diagnosis, long? value) => EnsureAssignmentState(diagnosis).CancellationRequestedBy = value;
    private void SetCancellationRequestedAt(Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(diagnosis).CancellationRequestedAt = value;
    private void SetCancellationDecisionBy(Diagnosis diagnosis, long? value) => EnsureAssignmentState(diagnosis).CancellationDecisionBy = value;
    private void SetCancellationDecisionAt(Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(diagnosis).CancellationDecisionAt = value;
    private void SetCancellationDecisionNote(Diagnosis diagnosis, string? value) => EnsureAssignmentState(diagnosis).CancellationDecisionNote = value;
    private void SetSupportRequestedBy(Diagnosis diagnosis, long? value) => EnsureAssignmentState(diagnosis).SupportRequestedBy = value;
    private void SetSupportRequestedAt(Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(diagnosis).SupportRequestedAt = value;
    private void SetSupportRequestNote(Diagnosis diagnosis, string? value) => EnsureAssignmentState(diagnosis).SupportRequestNote = value;
    private void SetTeamLeaderApprovalStatus(Diagnosis diagnosis, string? value) => EnsureAssignmentState(diagnosis).TeamLeaderApprovalStatus = value;
    private void SetFinalApprovalStatus(Diagnosis diagnosis, string? value) => EnsureAssignmentState(diagnosis).FinalApprovalStatus = value;
    private void SetForemanRejectReason(Diagnosis diagnosis, string? value) => EnsureAssignmentState(diagnosis).ForemanRejectReason = value;
    private void SetApprovedByForemanId(Diagnosis diagnosis, long? value) => EnsureAssignmentState(diagnosis).ApprovedByForemanId = value;
    private void SetApprovedByForemanAt(Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(diagnosis).ApprovedByForemanAt = value;
    private void SetApprovedByTeamLeaderId(Diagnosis diagnosis, long? value) => EnsureAssignmentState(diagnosis).ApprovedByTeamLeaderId = value;
    private void SetApprovedByTeamLeaderAt(Diagnosis diagnosis, DateTime? value) => EnsureAssignmentState(diagnosis).ApprovedByTeamLeaderAt = value;
    private void SetTeamLeaderNote(Diagnosis diagnosis, string? value) => EnsureAssignmentState(diagnosis).TeamLeaderNote = value;
    private void SetForemanNote(Diagnosis diagnosis, string? value) => EnsureAssignmentState(diagnosis).ForemanNote = value;
    private void SetRequiresInsuranceProof(Diagnosis diagnosis, bool value) => EnsureAssignmentState(diagnosis).RequiresInsuranceProof = value;
    private void SetProofImageUrl(Diagnosis diagnosis, string? value) => EnsureAssignmentState(diagnosis).ProofImageUrl = value;

    public long? ReadAssignedTeamId(Diagnosis diagnosis) => GetAssignedTeamId(diagnosis);
    public long? ReadTechnicianId(Diagnosis diagnosis) => GetTechnicianId(diagnosis);
    public long? ReadAssistantTechnicianId(Diagnosis diagnosis) => GetAssistantTechnicianId(diagnosis);
    public long? ReadAssignedBayId(Diagnosis diagnosis) => GetAssignedBayId(diagnosis);
    public DateTime? ReadCancellationRequestedAt(Diagnosis diagnosis) => GetCancellationRequestedAt(diagnosis);
    public DateTime? ReadCancellationDecisionAt(Diagnosis diagnosis) => GetCancellationDecisionAt(diagnosis);
    public string? ReadTeamLeaderApprovalStatus(Diagnosis diagnosis) => GetTeamLeaderApprovalStatus(diagnosis);
    public string? ReadFinalApprovalStatus(Diagnosis diagnosis) => GetFinalApprovalStatus(diagnosis);
    public int ReadTeamLeaderApprovalPendingMinutes(Diagnosis diagnosis, DateTime? now = null)
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

    public bool IsTeamLeaderApprovalWarning(Diagnosis diagnosis, long? executeBranchId = null, DateTime? now = null)
    {
        if (IsTeamLeaderApprovalEscalated(diagnosis, executeBranchId, now))
        {
            return false;
        }

        var finalStatus = (GetFinalApprovalStatus(diagnosis) ?? string.Empty).Trim();
        if (string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.REJECTED), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var teamLeaderStatus = (GetTeamLeaderApprovalStatus(diagnosis) ?? string.Empty).Trim();
        if (string.Equals(teamLeaderStatus, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ReadTeamLeaderApprovalPendingMinutes(diagnosis, now) >= GetTeamLeaderApprovalWarningMinutes(executeBranchId);
    }

    public bool IsTeamLeaderApprovalEscalated(Diagnosis diagnosis, long? executeBranchId = null, DateTime? now = null)
    {
        var finalStatus = (GetFinalApprovalStatus(diagnosis) ?? string.Empty).Trim();
        if (string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.REJECTED), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var teamLeaderStatus = (GetTeamLeaderApprovalStatus(diagnosis) ?? string.Empty).Trim();
        if (string.Equals(teamLeaderStatus, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ReadTeamLeaderApprovalPendingMinutes(diagnosis, now) >= GetTeamLeaderApprovalEscalationMinutes(executeBranchId);
    }

    public string? ReadForemanRejectReason(Diagnosis diagnosis) => GetForemanRejectReason(diagnosis);
    public long? ReadApprovedByForemanId(Diagnosis diagnosis) => GetApprovedByForemanId(diagnosis);
    public DateTime? ReadApprovedByForemanAt(Diagnosis diagnosis) => GetApprovedByForemanAt(diagnosis);
    public long? ReadApprovedByTeamLeaderId(Diagnosis diagnosis) => GetApprovedByTeamLeaderId(diagnosis);
    public DateTime? ReadApprovedByTeamLeaderAt(Diagnosis diagnosis) => GetApprovedByTeamLeaderAt(diagnosis);
    public string? ReadTeamLeaderNote(Diagnosis diagnosis) => GetTeamLeaderNote(diagnosis);
    public string? ReadForemanNote(Diagnosis diagnosis) => GetForemanNote(diagnosis);
    public bool ReadRequiresInsuranceProof(Diagnosis diagnosis) => GetRequiresInsuranceProof(diagnosis);
    public string? ReadProofImageUrl(Diagnosis diagnosis) => GetProofImageUrl(diagnosis);
    public long? ReadDiagnosisAssignmentId(Diagnosis diagnosis)
    {
        var assignmentId = EnsureAssignmentState(diagnosis).Id;
        return assignmentId > 0 ? assignmentId : null;
    }
}
