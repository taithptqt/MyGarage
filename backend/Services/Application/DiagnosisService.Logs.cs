using Microsoft.EntityFrameworkCore;
using MyGarage.Models;

namespace MyGarage.Services;

public partial class DiagnosisService
{
    private const string DiagnoseLogEventCreated = "CREATED";
    private const string DiagnoseLogEventAssigned = "ASSIGNED";
    private const string DiagnoseLogEventStarted = "STARTED";
    private const string DiagnoseLogEventSupportRequested = "SUPPORT_REQUESTED";
    private const string DiagnoseLogEventSupportSelection = "SUPPORT_SELECTION";
    private const string DiagnoseLogEventCancellationRequested = "CANCELLATION_REQUESTED";
    private const string DiagnoseLogEventCancellationApproved = "CANCELLATION_APPROVED";
    private const string DiagnoseLogEventCancellationRejected = "CANCELLATION_REJECTED";
    private const string DiagnoseLogEventCanceled = "CANCELED";
    private const string DiagnoseLogEventCompleted = "COMPLETED";
    private const string DiagnoseLogEventArchived = "ARCHIVED";

    private DiagnosisAssignment BuildDiagnosisAssignment(
        Diagnosis diagnosis,
        string eventType,
        string? oldStatus = null,
        long? createdBy = null,
        string? note = null,
        string? cancelReason = null)
    {
        var createdAt = diagnosis.UpdatedAt ?? diagnosis.CreatedAt ?? VietnamTime.Now;
        return new DiagnosisAssignment
        {
            DiagnosisId = diagnosis.Id,
            AssignedTeamId = diagnosis.AssignedTeamId,
            TechnicianId = diagnosis.TechnicianId,
            AssistantTechnicianId = diagnosis.AssistantTechnicianId,
            AssignedBayId = diagnosis.AssignedBayId,
            Status = diagnosis.Status,
            AssignedAt = diagnosis.AssignedAt,
            TechnicianAcceptedAt = diagnosis.TechnicianAcceptedAt,
            TechnicianCompletedAt = diagnosis.TechnicianCompletedAt,
            ApprovedByTeamLeaderAt = diagnosis.ApprovedByTeamLeaderAt,
            TeamLeaderNote = diagnosis.TeamLeaderNote,
            ApprovedByForemanId = diagnosis.ApprovedByForemanId,
            ApprovedByForemanAt = diagnosis.ApprovedByForemanAt,
            ForemanNote = diagnosis.ForemanNote,
            CancellationStatus = diagnosis.CancellationStatus,
            CancellationReason = string.IsNullOrWhiteSpace(cancelReason) ? diagnosis.CancellationReason : cancelReason,
            CancellationRequestedBy = diagnosis.CancellationRequestedBy,
            CancellationRequestedAt = diagnosis.CancellationRequestedAt,
            CancellationDecisionBy = diagnosis.CancellationDecisionBy,
            CancellationDecisionAt = diagnosis.CancellationDecisionAt,
            CancellationDecisionNote = diagnosis.CancellationDecisionNote,
            SupportRequestedBy = diagnosis.SupportRequestedBy,
            SupportRequestedAt = diagnosis.SupportRequestedAt,
            SupportRequestNote = diagnosis.SupportRequestNote,
            EventType = eventType,
            OldStatus = oldStatus,
            Note = ComposeAssignmentNote(note, diagnosis.DispatchCollaborationMeta),
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    private static string? ComposeAssignmentNote(string? note, string? dispatchCollaborationMeta)
    {
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var normalizedMeta = string.IsNullOrWhiteSpace(dispatchCollaborationMeta) ? null : dispatchCollaborationMeta.Trim();

        if (string.IsNullOrWhiteSpace(normalizedMeta))
        {
            return normalizedNote;
        }

        if (string.IsNullOrWhiteSpace(normalizedNote))
        {
            return normalizedMeta;
        }

        return normalizedNote.Contains(normalizedMeta, StringComparison.Ordinal)
            ? normalizedNote
            : $"{normalizedNote} {normalizedMeta}";
    }

    private void AppendDiagnosisAssignments(IEnumerable<DiagnosisAssignment> logs)
    {
        var items = logs?.Where(x => x != null).ToList() ?? new List<DiagnosisAssignment>();
        if (items.Count == 0)
        {
            return;
        }

        _context.DiagnosisAssignments.AddRange(items);
    }

    private async Task ApplyLatestLogOverlayAsync(IReadOnlyList<Diagnosis> diagnoses)
    {
        if (diagnoses == null || diagnoses.Count == 0)
        {
            return;
        }

        var diagnosisIds = diagnoses.Select(x => x.Id).Distinct().ToList();
        var latestLogs = await _context.DiagnosisAssignments.AsNoTracking()
            .Where(x => diagnosisIds.Contains(x.DiagnosisId))
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        var latestLogByDiagnosisId = latestLogs
            .GroupBy(x => x.DiagnosisId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var diagnosis in diagnoses)
        {
            if (!latestLogByDiagnosisId.TryGetValue(diagnosis.Id, out var latestLog))
            {
                continue;
            }

            diagnosis.AssignedTeamId = latestLog.AssignedTeamId;
            diagnosis.TechnicianId = latestLog.TechnicianId;
            diagnosis.AssistantTechnicianId = latestLog.AssistantTechnicianId;
            diagnosis.AssignedBayId = latestLog.AssignedBayId;
            diagnosis.AssignedAt = latestLog.AssignedAt;
            diagnosis.TechnicianAcceptedAt = latestLog.TechnicianAcceptedAt;
            diagnosis.TechnicianCompletedAt = latestLog.TechnicianCompletedAt;
            diagnosis.SupportRequestedBy = latestLog.SupportRequestedBy;
            diagnosis.SupportRequestedAt = latestLog.SupportRequestedAt;
            diagnosis.SupportRequestNote = latestLog.SupportRequestNote;
            diagnosis.CancellationStatus = latestLog.CancellationStatus;
            diagnosis.CancellationReason = latestLog.CancellationReason;
            diagnosis.CancellationRequestedBy = latestLog.CancellationRequestedBy;
            diagnosis.CancellationRequestedAt = latestLog.CancellationRequestedAt;
            diagnosis.CancellationDecisionBy = latestLog.CancellationDecisionBy;
            diagnosis.CancellationDecisionAt = latestLog.CancellationDecisionAt;
            diagnosis.CancellationDecisionNote = latestLog.CancellationDecisionNote;
            diagnosis.ApprovedByForemanId = latestLog.ApprovedByForemanId;
            diagnosis.ApprovedByForemanAt = latestLog.ApprovedByForemanAt;
            diagnosis.ApprovedByTeamLeaderAt = latestLog.ApprovedByTeamLeaderAt;
            diagnosis.TeamLeaderNote = latestLog.TeamLeaderNote;
            diagnosis.ForemanNote = latestLog.ForemanNote;
        }
    }
}
