using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisQueueCommandFacade
    {
        Task<DiagnosisCommandResult<DiagnosisResponseDto>> RequestDiagnosisAsync(long serviceOrderId, DiagnosisQueueCreateRequestDto? request);
        Task<DiagnosisCommandResult<DiagnosisResponseDto>> AssignRequestAsync(long serviceOrderId, DiagnosisAcceptRequestDto? request);
        Task<DiagnosisCommandResult<DiagnosisResponseDto>> StartDiagnosisAsync(long serviceOrderId, DiagnosisStartRequestDto? request);
        Task<DiagnosisCommandResult> DeleteRequestAsync(long serviceOrderId);
        Task<DiagnosisCommandResult> RequestSupportAsync(long serviceOrderId, DiagnosisSupportRequestDto? request);
        Task<DiagnosisCommandResult> RequestCancelAssignmentAsync(long serviceOrderId, DiagnosisCancelRequestDto? request);
        Task<DiagnosisCommandResult> DecideCancelAssignmentAsync(long serviceOrderId, DiagnosisCancelDecisionDto? request);
        Task<DiagnosisCommandResult<DiagnosisResponseDto>> ManagerOverrideAssignmentAsync(long serviceOrderId, DiagnosisManagerOverrideDto? request);
        Task<DiagnosisCommandResult<DiagnosisResponseDto>> SelectSupportingTeamAssignmentAsync(long serviceOrderId, DiagnosisSupportingTeamSelectionRequestDto? request);
    }
}
