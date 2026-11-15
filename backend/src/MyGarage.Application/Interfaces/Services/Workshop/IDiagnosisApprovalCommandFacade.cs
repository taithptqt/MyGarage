using System.Threading.Tasks;
using MyGarage.DTOs.Requests;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisApprovalCommandFacade
    {
        Task<DiagnosisCommandResult> ApproveTechnicalAsync(long serviceOrderId, DiagnosisTechnicalApprovalRequestDto? request);
        Task<DiagnosisCommandResult> RejectTechnicalAsync(long serviceOrderId, DiagnosisTechnicalApprovalRequestDto? request);
        Task<DiagnosisCommandResult> CloneRejectedVersionAsync(long serviceOrderId, DiagnosisCloneVersionRequestDto? request);
        Task<DiagnosisCommandResult> ApproveFinalAsync(long serviceOrderId, DiagnosisFinalApprovalRequestDto? request);
        Task<DiagnosisCommandResult> ForemanDecisionAsync(long serviceOrderId, DiagnosisForemanDecisionRequestDto? request);
    }
}
