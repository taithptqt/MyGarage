using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisCommandHelpers;

namespace MyGarage.Services
{
    public class DiagnosisQueueAssignmentService : IDiagnosisQueueAssignmentService
    {
        private readonly IDiagnosisService _diagnosisService;
        private readonly IDiagnosisResponseCommandService _responseService;
        private readonly IDiagnosisAssignmentCollaborationService _collaborationService;

        public DiagnosisQueueAssignmentService(
            IDiagnosisService diagnosisService,
            IDiagnosisResponseCommandService responseService,
            IDiagnosisAssignmentCollaborationService collaborationService)
        {
            _diagnosisService = diagnosisService;
            _responseService = responseService;
            _collaborationService = collaborationService;
        }

        public async Task<DiagnosisCommandResult<DiagnosisResponseDto>> RequestDiagnosisAsync(
            long serviceOrderId,
            DiagnosisQueueCreateRequestDto? request)
        {
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.NotFound("Không tìm thấy lệnh sửa chữa.");
            }

            var hasSignedCheckIn = await _diagnosisService.HasSignedCheckInAsync(serviceOrderId);
            if (!hasSignedCheckIn)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Chỉ được yêu cầu chẩn đoán sau khi khách đã ký check-in.");
            }

            var allDiagnosis = await _diagnosisService.GetAllByServiceOrderAsync(serviceOrderId);
            if (allDiagnosis.Any(x => x.Status == DiagnosisStatusCompleted))
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Lệnh này đã có kết quả chẩn đoán hoàn tất.");
            }

            var activeDiagnosis = allDiagnosis
                .Where(x =>
                    !string.Equals(x.Status, DiagnosisStatusCompleted, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(x.Status, DiagnosisStatusCanceled, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var recalledDiagnoses = allDiagnosis
                .Where(x => string.Equals(x.Status, DiagnosisStatusCanceled, StringComparison.OrdinalIgnoreCase))
                .Where(x => !IsFinalDecisionMade(_diagnosisService.ReadFinalApprovalStatus(x)))
                .ToList();

            if (activeDiagnosis.Count > 0)
            {
                await _diagnosisService.ArchiveDiagnosesAsSupersededAsync(activeDiagnosis);
            }

            var dispatchTag = NormalizeDispatchTag(request?.DispatchTag);
            var symptoms = FilterSymptomsByDispatchTag(
                ExtractSymptomsFromNotes(serviceOrder.CustomerNotes),
                dispatchTag);
            var intakeSuggestion = await _diagnosisService.BuildIntakeSuggestionAsync(
                serviceOrderId,
                dispatchTag,
                request?.BufferMinutes,
                request?.IntakePackageCode);

            var bypassConflictGuard = request?.BypassConflictGuard ?? false;
            if (!intakeSuggestion.CanApply && !bypassConflictGuard)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.Conflict(new
                {
                    message = "Khung gio bi xung dot lich booking. Bat bypass de tiep tuc.",
                    conflictLevel = intakeSuggestion.ConflictLevel,
                    conflictMinutes = intakeSuggestion.ConflictMinutes,
                    finalPackageCode = intakeSuggestion.FinalPackageCode,
                    t1EstimatedHandover = intakeSuggestion.T1EstimatedHandover,
                    requiresDispatchReview = true
                });
            }

            if (intakeSuggestion.RequiresCustomerConsent && request?.CustomerConsentAccepted != true)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest(
                    "Gói chẩn đoán 30p/60p cần xác nhận đồng ý của khách trước khi tạo yêu cầu.");
            }

            if (!intakeSuggestion.SuggestedTeamId.HasValue || intakeSuggestion.SuggestedTeamId.Value <= 0)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.Conflict(new
                {
                    message = "Không thể tự động xác định tổ đúng chuyên môn. Không tạo yêu cầu chẩn đoán.",
                    requiresDispatchReview = true,
                    dispatchTag = intakeSuggestion.DispatchTag,
                    conflictLevel = intakeSuggestion.ConflictLevel,
                    conflictMinutes = intakeSuggestion.ConflictMinutes
                });
            }

            var resolvedTeamId = intakeSuggestion.SuggestedTeamId.Value;
            var resolvedDiagnoseBayId = await _diagnosisService.ResolveDiagnoseBayIdForBranchAsync(
                serviceOrder.ExecuteBranchId,
                intakeSuggestion.SuggestedBayId);
            var scheduledSlot = await _diagnosisService.ScheduleDiagnosisQueueSlotAsync(
                serviceOrderId,
                intakeSuggestion.PackageEffectiveMinutes,
                bufferMinutes: 15,
                preferredBayId: resolvedDiagnoseBayId);
            var finalAssignedBayId = scheduledSlot?.BayId ?? resolvedDiagnoseBayId;
            var actorStaffId = serviceOrder.ReceptionistId
                ?? serviceOrder.ServiceAdvisorId
                ?? serviceOrder.UpdatedBy
                ?? serviceOrder.CreatedBy;

            var newDiagnosis = recalledDiagnoses.Count > 0 && activeDiagnosis.Count == 0
                ? await _diagnosisService.RequeueRecalledDiagnosesAsync(
                    recalledDiagnoses,
                    assignedTeamId: resolvedTeamId,
                    assignedBayId: finalAssignedBayId,
                    dispatchTag: dispatchTag,
                    assignmentNote: request?.AssignmentNote,
                    actorStaffId: actorStaffId)
                : await _diagnosisService.CreatePendingDiagnosesAsync(
                    serviceOrderId,
                    technicianId: null,
                    symptoms,
                    dispatchTag,
                    assignedTeamId: resolvedTeamId,
                    assignedBayId: finalAssignedBayId,
                    assignmentNote: request?.AssignmentNote);

            serviceOrder.EstimatedHandover = intakeSuggestion.T1EstimatedHandover;
            await _diagnosisService.RecordIntakeSnapshotAsync(
                serviceOrder,
                intakeSuggestion,
                dispatchTag,
                bypassConflictGuard: request?.BypassConflictGuard ?? false,
                autoAssign: request?.AutoAssign ?? false,
                customerConsentAccepted: request?.CustomerConsentAccepted == true,
                customerConsentChannel: request?.CustomerConsentChannel,
                customerConsentNote: request?.CustomerConsentNote,
                diagnoses: newDiagnosis);

            var dto = await _responseService.BuildSingleResponseDtoAsync(newDiagnosis.First(), applyAssignmentState: false);
            return DiagnosisCommandResult<DiagnosisResponseDto>.Ok(dto);
        }

        public async Task<DiagnosisCommandResult<DiagnosisResponseDto>> AssignRequestAsync(
            long serviceOrderId,
            DiagnosisAcceptRequestDto? request)
        {
            if (request == null || request.TechnicianId <= 0)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thiếu kỹ thuật viên nhận yêu cầu.");
            }

            if (request.AssistantTechnicianId.HasValue && request.AssistantTechnicianId.Value == request.TechnicianId)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thợ phụ không được trùng với thợ chính.");
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Yêu cầu chẩn đoán chưa được tạo.");
            }

            if (activeDiagnosis.Any(x => x.Status == DiagnosisStatusCompleted))
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Chẩn đoán đã hoàn tất, không thể nhận lại.");
            }

            if (!request.IsManagerOverride &&
                activeDiagnosis.Any(x => x.Status == DiagnosisStatusAssigned && _diagnosisService.ReadTechnicianId(x) != request.TechnicianId))
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.Conflict(new { message = "Yêu cầu đã được kỹ thuật viên khác nhận." });
            }

            var resolvedTeamId = request.TeamId ?? await _diagnosisService.ResolveTeamIdForTechnicianAsync(request.TechnicianId);
            var primaryError = await ValidatePrimaryAndAssistantTechniciansAsync(
                _diagnosisService,
                request.TechnicianId,
                request.TeamId,
                resolvedTeamId,
                request.AssistantTechnicianId,
                teamMismatchMessage: "Kỹ thuật viên không thuộc tổ được chọn.");
            if (primaryError != null)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest(primaryError);
            }

            var normalizedSupportingAssignments = NormalizeSupportingAssignments(
                request.SupportingTeamIds,
                request.SupportingAssignments,
                resolvedTeamId);
            var normalizedSupportingTeamIds = normalizedSupportingAssignments
                .Select(x => x.TeamId)
                .Distinct()
                .ToList();

            if (normalizedSupportingTeamIds.Count > 0 && !resolvedTeamId.HasValue)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Khi có tổ phối hợp, bắt buộc chọn tổ chính.");
            }

            var requiresServiceOrderContext = request.BayId.HasValue || normalizedSupportingTeamIds.Count > 0;
            ServiceOrder? serviceOrder = null;
            if (requiresServiceOrderContext)
            {
                serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
                if (serviceOrder == null)
                {
                    return DiagnosisCommandResult<DiagnosisResponseDto>.NotFound("Không tìm thấy lệnh sửa chữa.");
                }
            }

            if (normalizedSupportingTeamIds.Count > 0)
            {
                var supportingError = await ValidateSupportingAssignmentsWithTechniciansAsync(
                    _diagnosisService,
                    normalizedSupportingAssignments,
                    serviceOrder?.ExecuteBranchId);
                if (supportingError != null)
                {
                    return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest(supportingError);
                }
            }

            if (request.BayId.HasValue)
            {
                var bayInBranch = await _diagnosisService.IsBayInBranchAsync(request.BayId.Value, serviceOrder!.ExecuteBranchId);
                if (!bayInBranch)
                {
                    return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Cầu nâng không thuộc chi nhánh xử lý.");
                }
            }

            if (!request.IsManagerOverride)
            {
                var dispatchTag = activeDiagnosis
                    .Select(x => x.DispatchTag)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                var dispatchTeamIds = new List<long>();
                if (resolvedTeamId.HasValue && resolvedTeamId.Value > 0)
                {
                    dispatchTeamIds.Add(resolvedTeamId.Value);
                }

                dispatchTeamIds.AddRange(normalizedSupportingTeamIds);

                foreach (var dispatchTeamId in dispatchTeamIds.Distinct())
                {
                    var isCompatible = await _diagnosisService.IsDispatchTagCompatibleAsync(dispatchTag, dispatchTeamId);
                    if (!isCompatible)
                    {
                        return DiagnosisCommandResult<DiagnosisResponseDto>.Conflict(new { message = "Có tổ được chọn không đúng chuyên môn của lệnh chẩn đoán." });
                    }
                }

                if (request.BayId.HasValue)
                {
                    foreach (var dispatchTeamId in dispatchTeamIds.Distinct())
                    {
                        var compatibility = await _diagnosisService.ValidateTeamBayCompatibilityAsync(
                            serviceOrder!.Id,
                            dispatchTeamId,
                            request.BayId.Value);
                        if (!compatibility.IsCompatible)
                        {
                            return DiagnosisCommandResult<DiagnosisResponseDto>.Conflict(new { message = compatibility.Reason });
                        }
                    }
                }
            }

            await _diagnosisService.AssignDiagnosesAsync(
                activeDiagnosis,
                request.TechnicianId,
                request.AssistantTechnicianId,
                resolvedTeamId,
                normalizedSupportingTeamIds,
                normalizedSupportingAssignments,
                request.BayId,
                request.IsManagerOverride);

            var latest = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();
            var dto = await _responseService.BuildSingleResponseDtoAsync(latest);
            return DiagnosisCommandResult<DiagnosisResponseDto>.Ok(dto);
        }

        public async Task<DiagnosisCommandResult<DiagnosisResponseDto>> StartDiagnosisAsync(
            long serviceOrderId,
            DiagnosisStartRequestDto? request)
        {
            if (request == null || request.TechnicianId <= 0)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thiếu thông tin kỹ thuật viên bắt đầu chẩn đoán.");
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.NotFound("Không tìm thấy lệnh sửa chữa.");
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Không có lệnh chẩn đoán đang hoạt động.");
            }

            var isAssignedMember = activeDiagnosis.Any(x =>
                _diagnosisService.ReadTechnicianId(x) == request.TechnicianId ||
                _diagnosisService.ReadAssistantTechnicianId(x).GetValueOrDefault() == request.TechnicianId);
            if (!isAssignedMember)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.Forbid();
            }

            if (request.BayId.HasValue && serviceOrder.ExecuteBranchId > 0)
            {
                var bayInBranch = await _diagnosisService.IsBayInBranchAsync(request.BayId.Value, serviceOrder.ExecuteBranchId);
                if (!bayInBranch)
                {
                    return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Cầu nâng được chọn không thuộc chi nhánh đang xử lý.");
                }

                var assignedTeamIds = activeDiagnosis
                    .Select(x => _diagnosisService.ReadAssignedTeamId(x))
                    .Where(x => x.HasValue && x.Value > 0)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();
                var hasPreApprovedBayAssignment = activeDiagnosis.Any(x => _diagnosisService.ReadAssignedBayId(x).GetValueOrDefault() == request.BayId.Value);

                if (!hasPreApprovedBayAssignment)
                {
                    foreach (var assignedTeamId in assignedTeamIds)
                    {
                        var (isCompatible, reason) = await _diagnosisService.ValidateTeamBayCompatibilityAsync(
                            serviceOrderId,
                            assignedTeamId,
                            request.BayId.Value);
                        if (!isCompatible)
                        {
                            return DiagnosisCommandResult<DiagnosisResponseDto>.Conflict(new { message = reason });
                        }
                    }
                }
            }

            await _diagnosisService.StartDiagnosesAsync(activeDiagnosis, request.TechnicianId, request.BayId);
            await _diagnosisService.UpdateServiceOrderStatusAsync(serviceOrder, ServiceOrderStatusDiagnosing);

            var latest = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();
            var dto = await _responseService.BuildSingleResponseDtoAsync(latest);
            return DiagnosisCommandResult<DiagnosisResponseDto>.Ok(dto);
        }

        public async Task<DiagnosisCommandResult> DeleteRequestAsync(long serviceOrderId)
        {
            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return DiagnosisCommandResult.NotFound("Không tìm thấy lệnh sửa chữa.");
            }

            var workflowDiagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (workflowDiagnoses.Count == 0)
            {
                return DiagnosisCommandResult.Ok(new { message = "Không có phiếu chẩn đoán để hủy." });
            }

            var cancellableDiagnoses = workflowDiagnoses
                .Where(x =>
                    !string.Equals(x.Status, DiagnosisStatusCanceled, StringComparison.OrdinalIgnoreCase) &&
                    !IsFinalDecisionMade(_diagnosisService.ReadFinalApprovalStatus(x)))
                .ToList();

            if (cancellableDiagnoses.Count == 0)
            {
                return DiagnosisCommandResult.BadRequest("Phiếu chẩn đoán đã được quản đốc quyết định, không thể hủy.");
            }

            await _diagnosisService.RecallDiagnosesAsync(serviceOrder, cancellableDiagnoses);
            return DiagnosisCommandResult.Ok(new { message = "Đã hủy phiếu chẩn đoán." });
        }

        public Task<DiagnosisCommandResult> RequestSupportAsync(long serviceOrderId, DiagnosisSupportRequestDto? request) =>
            _collaborationService.RequestSupportAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult> RequestCancelAssignmentAsync(long serviceOrderId, DiagnosisCancelRequestDto? request) =>
            _collaborationService.RequestCancelAssignmentAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult> DecideCancelAssignmentAsync(long serviceOrderId, DiagnosisCancelDecisionDto? request) =>
            _collaborationService.DecideCancelAssignmentAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult<DiagnosisResponseDto>> ManagerOverrideAssignmentAsync(long serviceOrderId, DiagnosisManagerOverrideDto? request) =>
            _collaborationService.ManagerOverrideAssignmentAsync(serviceOrderId, request);

        public Task<DiagnosisCommandResult<DiagnosisResponseDto>> SelectSupportingTeamAssignmentAsync(long serviceOrderId, DiagnosisSupportingTeamSelectionRequestDto? request) =>
            _collaborationService.SelectSupportingTeamAssignmentAsync(serviceOrderId, request);
    }
}
