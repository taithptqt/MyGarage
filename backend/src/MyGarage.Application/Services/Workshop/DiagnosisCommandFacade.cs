using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class DiagnosisCommandFacade : IDiagnosisCommandFacade
    {
        private readonly IDiagnosisResponseCommandService _responseService;
        private readonly IDiagnosisQueueAssignmentService _queueAssignmentService;
        private readonly IDiagnosisApprovalCommandService _approvalService;

        public DiagnosisCommandFacade(
            IDiagnosisResponseCommandService responseService,
            IDiagnosisQueueAssignmentService queueAssignmentService,
            IDiagnosisApprovalCommandService approvalService)
        {
            _responseService = responseService;
            _queueAssignmentService = queueAssignmentService;
            _approvalService = approvalService;
        }

        public Task<IReadOnlyList<DiagnosisResponseDto>> GetAllAsync(long? serviceOrderId, bool requestOnly, long? teamId) =>
            _responseService.GetAllAsync(serviceOrderId, requestOnly, teamId);

        public Task<DiagnosisResponseDto?> GetByIdAsync(long id) =>
            _responseService.GetByIdAsync(id);

        public Task<DiagnosisResponseDto> CreateAsync(DiagnosisCreateDto itemDto) =>
            _responseService.CreateAsync(itemDto);

        public Task<DiagnosisResponseDto?> UpdateAsync(long id, DiagnosisUpdateDto itemDto) =>
            _responseService.UpdateAsync(id, itemDto);

        public Task<DiagnosisCommandResult<DiagnosisResponseDto>> CompleteAsync(
            long id,
            string? symptom,
            string? rootCause,
            string? severity) =>
            _responseService.CompleteAsync(id, symptom, rootCause, severity);

        public Task<DiagnosisCommandResult<DiagnosisResponseDto>> RequestDiagnosisAsync(
            long serviceOrderId,
            DiagnosisQueueCreateRequestDto? request) =>
            _queueAssignmentService.RequestDiagnosisAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult<DiagnosisResponseDto>> AssignRequestAsync(
            long serviceOrderId,
            DiagnosisAcceptRequestDto? request) =>
            _queueAssignmentService.AssignRequestAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult<DiagnosisResponseDto>> StartDiagnosisAsync(
            long serviceOrderId,
            DiagnosisStartRequestDto? request) =>
            _queueAssignmentService.StartDiagnosisAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult> DeleteRequestAsync(long serviceOrderId) =>
            _queueAssignmentService.DeleteRequestAsync(serviceOrderId);

        public Task<DiagnosisCommandResult> ApproveTechnicalAsync(
            long serviceOrderId,
            DiagnosisTechnicalApprovalRequestDto? request) =>
            _approvalService.ApproveTechnicalAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult> RejectTechnicalAsync(
            long serviceOrderId,
            DiagnosisTechnicalApprovalRequestDto? request) =>
            _approvalService.RejectTechnicalAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult> CloneRejectedVersionAsync(
            long serviceOrderId,
            DiagnosisCloneVersionRequestDto? request) =>
            _approvalService.CloneRejectedVersionAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult> ApproveFinalAsync(
            long serviceOrderId,
            DiagnosisFinalApprovalRequestDto? request) =>
            _approvalService.ApproveFinalAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult> ForemanDecisionAsync(
            long serviceOrderId,
            DiagnosisForemanDecisionRequestDto? request) =>
            _approvalService.ForemanDecisionAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult> RequestSupportAsync(
            long serviceOrderId,
            DiagnosisSupportRequestDto? request) =>
            _queueAssignmentService.RequestSupportAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult> RequestCancelAssignmentAsync(
            long serviceOrderId,
            DiagnosisCancelRequestDto? request) =>
            _queueAssignmentService.RequestCancelAssignmentAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult> DecideCancelAssignmentAsync(
            long serviceOrderId,
            DiagnosisCancelDecisionDto? request) =>
            _queueAssignmentService.DecideCancelAssignmentAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult<DiagnosisResponseDto>> ManagerOverrideAssignmentAsync(
            long serviceOrderId,
            DiagnosisManagerOverrideDto? request) =>
            _queueAssignmentService.ManagerOverrideAssignmentAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult<DiagnosisResponseDto>> SelectSupportingTeamAssignmentAsync(
            long serviceOrderId,
            DiagnosisSupportingTeamSelectionRequestDto? request) =>
            _queueAssignmentService.SelectSupportingTeamAssignmentAsync(serviceOrderId, request);
    }
}
