using System;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisApprovalReader
    {
        string? ReadTeamLeaderApprovalStatus(Diagnosis diagnosis);
        string? ReadFinalApprovalStatus(Diagnosis diagnosis);
        int ReadTeamLeaderApprovalPendingMinutes(Diagnosis diagnosis, DateTime? now = null);
        bool IsTeamLeaderApprovalWarning(Diagnosis diagnosis, long? executeBranchId = null, DateTime? now = null);
        bool IsTeamLeaderApprovalEscalated(Diagnosis diagnosis, long? executeBranchId = null, DateTime? now = null);
        string? ReadForemanRejectReason(Diagnosis diagnosis);
        long? ReadApprovedByForemanId(Diagnosis diagnosis);
        DateTime? ReadApprovedByForemanAt(Diagnosis diagnosis);
        long? ReadApprovedByTeamLeaderId(Diagnosis diagnosis);
        DateTime? ReadApprovedByTeamLeaderAt(Diagnosis diagnosis);
        string? ReadTeamLeaderNote(Diagnosis diagnosis);
        string? ReadForemanNote(Diagnosis diagnosis);
    }
}
