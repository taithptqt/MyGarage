using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisApprovalWorkflowService
    {
        Task RequestCancellationAsync(IEnumerable<Diagnosis> diagnoses, long requestedByStaffId, string reason);
        Task ApplyCancellationDecisionAsync(ServiceOrder serviceOrder, IEnumerable<Diagnosis> diagnoses, bool approve, long managerId, string? note);
        Task RecallDiagnosesAsync(ServiceOrder serviceOrder, IReadOnlyList<Diagnosis> activeDiagnoses);
        Task<IReadOnlyList<Diagnosis>> RequeueRecalledDiagnosesAsync(
            IEnumerable<Diagnosis> recalledDiagnoses,
            long? assignedTeamId,
            long? assignedBayId,
            string? dispatchTag = null,
            string? assignmentNote = null,
            long? actorStaffId = null);
        Task CompleteDiagnosisAsync(Diagnosis diagnosis);
        Task ApproveTechnicalAsync(IEnumerable<Diagnosis> diagnoses, long teamLeaderId, string? note);
        Task RejectTechnicalAsync(IEnumerable<Diagnosis> diagnoses, long teamLeaderId, string? reason);
        Task ApproveFinalAsync(IEnumerable<Diagnosis> diagnoses, long approverId, string? note, bool bypassTeamLeader = false, string? bypassAuditNote = null);
        Task RejectFinalAsync(IEnumerable<Diagnosis> diagnoses, long foremanId, string? note);
        Task<int> CloneRejectedDiagnosesVersionAsync(IEnumerable<Diagnosis> diagnoses, long actorId, string? note = null);
    }
}
