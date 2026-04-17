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
    private const string DiagnoseLogEventTechnicalApproved = "TECHNICAL_APPROVED";
    private const string DiagnoseLogEventTechnicalRejected = "TECHNICAL_REJECTED";
    private const string DiagnoseLogEventFinalApproved = "FINAL_APPROVED";
    private const string DiagnoseLogEventFinalApprovedBypass = "FINAL_APPROVED_BYPASS";

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
            AssignedTeamId = GetAssignedTeamId(diagnosis),
            TechnicianId = GetTechnicianId(diagnosis),
            AssistantTechnicianId = GetAssistantTechnicianId(diagnosis),
            AssignedBayId = GetAssignedBayId(diagnosis),
            Status = diagnosis.Status,
            AssignedAt = GetAssignedAt(diagnosis),
            TechnicianAcceptedAt = GetTechnicianAcceptedAt(diagnosis),
            TechnicianCompletedAt = GetTechnicianCompletedAt(diagnosis),
            CancellationStatus = GetCancellationStatus(diagnosis),
            CancellationReason = string.IsNullOrWhiteSpace(cancelReason) ? GetCancellationReason(diagnosis) : cancelReason,
            CancellationRequestedBy = GetCancellationRequestedBy(diagnosis),
            CancellationRequestedAt = GetCancellationRequestedAt(diagnosis),
            CancellationDecisionBy = GetCancellationDecisionBy(diagnosis),
            CancellationDecisionAt = GetCancellationDecisionAt(diagnosis),
            CancellationDecisionNote = GetCancellationDecisionNote(diagnosis),
            SupportRequestedBy = GetSupportRequestedBy(diagnosis),
            SupportRequestedAt = GetSupportRequestedAt(diagnosis),
            SupportRequestNote = GetSupportRequestNote(diagnosis),
            TeamLeaderApprovalStatus = GetTeamLeaderApprovalStatus(diagnosis),
            FinalApprovalStatus = GetFinalApprovalStatus(diagnosis),
            ForemanRejectReason = GetForemanRejectReason(diagnosis),
            ApprovedByForemanId = GetApprovedByForemanId(diagnosis),
            ApprovedByForemanAt = GetApprovedByForemanAt(diagnosis),
            ApprovedByTeamLeaderId = GetApprovedByTeamLeaderId(diagnosis),
            ApprovedByTeamLeaderAt = GetApprovedByTeamLeaderAt(diagnosis),
            TeamLeaderNote = GetTeamLeaderNote(diagnosis),
            ForemanNote = GetForemanNote(diagnosis),
            EventType = eventType,
            OldStatus = oldStatus,
            Note = ComposeAssignmentNote(note),
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    private static string? ComposeAssignmentNote(string? note)
    {
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return normalizedNote;
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
        await PreloadAssignmentStatesAsync(diagnoses);
    }
}


