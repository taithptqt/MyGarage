using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisAssignmentCollaborationService
    {
        Task<DiagnosisCommandResult> RequestSupportAsync(long serviceOrderId, DiagnosisSupportRequestDto? request);
        Task<DiagnosisCommandResult> RequestCancelAssignmentAsync(long serviceOrderId, DiagnosisCancelRequestDto? request);
        Task<DiagnosisCommandResult> DecideCancelAssignmentAsync(long serviceOrderId, DiagnosisCancelDecisionDto? request);
        Task<DiagnosisCommandResult<DiagnosisResponseDto>> ManagerOverrideAssignmentAsync(long serviceOrderId, DiagnosisManagerOverrideDto? request);
        Task<DiagnosisCommandResult<DiagnosisResponseDto>> SelectSupportingTeamAssignmentAsync(long serviceOrderId, DiagnosisSupportingTeamSelectionRequestDto? request);
    }
}
