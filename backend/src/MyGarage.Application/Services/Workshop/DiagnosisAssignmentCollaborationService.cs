using System;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using static MyGarage.Services.DiagnosisCommandHelpers;

namespace MyGarage.Services
{
    public class DiagnosisAssignmentCollaborationService : IDiagnosisAssignmentCollaborationService
    {
        private readonly IDiagnosisService _diagnosisService;
        private readonly IDiagnosisResponseCommandService _responseService;

        public DiagnosisAssignmentCollaborationService(
            IDiagnosisService diagnosisService,
            IDiagnosisResponseCommandService responseService)
        {
            _diagnosisService = diagnosisService;
            _responseService = responseService;
        }

        public async Task<DiagnosisCommandResult> RequestSupportAsync(
            long serviceOrderId,
            DiagnosisSupportRequestDto? request)
        {
            if (request == null || request.RequestedByStaffId <= 0)
            {
                return DiagnosisCommandResult.BadRequest("Thiếu thông tin người yêu cầu hỗ trợ.");
            }

            var requester = await _diagnosisService.GetStaffByIdAsync(request.RequestedByStaffId);
            if (requester == null)
            {
                return DiagnosisCommandResult.BadRequest("Người yêu cầu hỗ trợ không tồn tại.");
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return DiagnosisCommandResult.BadRequest("Không có lệnh chuẩn đoán đang hoạt động để yêu cầu hỗ trợ.");
            }

            long? primaryTeamId = activeDiagnosis
                .Select(x => _diagnosisService.ReadAssignedTeamId(x))
                .Where(x => x.HasValue && x.Value > 0)
                .FirstOrDefault();
            if (!primaryTeamId.HasValue || primaryTeamId.Value <= 0)
            {
                primaryTeamId = null;
            }

            var normalizedSupportingAssignments = NormalizeSupportingAssignments(
                teamIds: null,
                assignments: request.SupportingAssignments,
                primaryTeamId: primaryTeamId);

            if (normalizedSupportingAssignments.Count > 0 && !primaryTeamId.HasValue)
            {
                return DiagnosisCommandResult.BadRequest("Chỉ cho phép yêu cầu tổ phối hợp sau khi đã có tổ chính.");
            }

            if (normalizedSupportingAssignments.Count > 0)
            {
                var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
                if (serviceOrder == null)
                {
                    return DiagnosisCommandResult.NotFound("Không tìm thấy lệnh sửa chữa.");
                }

                foreach (var supportingAssignment in normalizedSupportingAssignments)
                {
                    if (supportingAssignment.RequestedTechnicianCount.GetValueOrDefault() <= 0)
                    {
                        return DiagnosisCommandResult.BadRequest("Mỗi tổ phối hợp phải có số lượng thợ yêu cầu lớn hơn 0.");
                    }

                    if (supportingAssignment.TechnicianId > 0 || supportingAssignment.AssistantTechnicianId.HasValue)
                    {
                        return DiagnosisCommandResult.BadRequest("Tổ trưởng tổ chính không được chọn thợ cho tổ phối hợp.");
                    }

                    var supportingTeam = await _diagnosisService.GetTeamByIdAsync(supportingAssignment.TeamId);
                    if (supportingTeam == null || (supportingTeam.IsDeleted.HasValue && supportingTeam.IsDeleted.Value))
                    {
                        return DiagnosisCommandResult.BadRequest($"Tổ phối hợp #{supportingAssignment.TeamId} không tồn tại.");
                    }

                    if (supportingTeam.BranchId != serviceOrder.ExecuteBranchId)
                    {
                        return DiagnosisCommandResult.BadRequest("Tổ phối hợp phải thuộc cùng chi nhánh với lệnh sửa chữa.");
                    }
                }
            }

            await _diagnosisService.MarkSupportRequestedAsync(
                activeDiagnosis,
                request.RequestedByStaffId,
                request.Note,
                normalizedSupportingAssignments);
            return DiagnosisCommandResult.Ok(new
            {
                message = "Đã ghi nhận yêu cầu hỗ trợ kỹ thuật.",
                serviceOrderId,
                requestedBy = request.RequestedByStaffId
            });
        }

        public async Task<DiagnosisCommandResult> RequestCancelAssignmentAsync(
            long serviceOrderId,
            DiagnosisCancelRequestDto? request)
        {
            if (request == null || request.RequestedByStaffId <= 0)
            {
                return DiagnosisCommandResult.BadRequest("Thiếu thông tin người yêu cầu hủy.");
            }

            var reason = (request.Reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return DiagnosisCommandResult.BadRequest("Bắt buộc nhập lý do hủy (Sai chuyên môn, Quá tải cầu, Thiếu thiết bị...).");
            }

            var requester = await _diagnosisService.GetStaffByIdAsync(request.RequestedByStaffId);
            if (requester == null)
            {
                return DiagnosisCommandResult.BadRequest("Người yêu cầu hủy không tồn tại.");
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return DiagnosisCommandResult.BadRequest("Không có lệnh chẩn đoán đang hoạt động để hủy.");
            }

            try
            {
                await _diagnosisService.RequestCancellationAsync(activeDiagnosis, request.RequestedByStaffId, reason);
                return DiagnosisCommandResult.Ok(new { message = "Đã gửi lệnh hủy sang trạng thái chờ duyệt của quản đốc." });
            }
            catch (InvalidOperationException ex)
            {
                return DiagnosisCommandResult.BadRequest(ex.Message);
            }
        }

        public async Task<DiagnosisCommandResult> DecideCancelAssignmentAsync(
            long serviceOrderId,
            DiagnosisCancelDecisionDto? request)
        {
            if (request == null || request.ManagerId <= 0)
            {
                return DiagnosisCommandResult.BadRequest("Thiếu thông tin quản đốc phê duyệt.");
            }

            var manager = await _diagnosisService.GetStaffByIdAsync(request.ManagerId);
            if (manager == null)
            {
                return DiagnosisCommandResult.BadRequest("Quản đốc không tồn tại.");
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return DiagnosisCommandResult.NotFound("Không tìm thấy lệnh sửa chữa.");
            }

            var allDiagnosis = await _diagnosisService.GetAllByServiceOrderAsync(serviceOrderId);
            var pendingCancellation = allDiagnosis
                .Where(x => _diagnosisService.ReadCancellationRequestedAt(x).HasValue && !_diagnosisService.ReadCancellationDecisionAt(x).HasValue)
                .ToList();

            if (pendingCancellation.Count == 0)
            {
                return DiagnosisCommandResult.BadRequest("Không có yêu cầu hủy nào đang chờ duyệt.");
            }

            await _diagnosisService.ApplyCancellationDecisionAsync(serviceOrder, pendingCancellation, request.Approve, request.ManagerId, request.Note);

            return DiagnosisCommandResult.Ok(new
            {
                message = request.Approve
                    ? "Quản đốc đã đồng ý hủy, lệnh được đưa về hàng chờ phân công lại."
                    : "Quản đốc từ chối hủy lệnh, tổ tiếp tục xử lý xe.",
                approved = request.Approve
            });
        }

        public async Task<DiagnosisCommandResult<DiagnosisResponseDto>> ManagerOverrideAssignmentAsync(
            long serviceOrderId,
            DiagnosisManagerOverrideDto? request)
        {
            if (request == null || request.ManagerId <= 0 || request.TechnicianId <= 0)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thiếu thông tin override của quản đốc.");
            }

            if (request.AssistantTechnicianId.HasValue && request.AssistantTechnicianId.Value == request.TechnicianId)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thợ phụ không được trùng với thợ chính.");
            }

            var manager = await _diagnosisService.GetStaffByIdAsync(request.ManagerId);
            if (manager == null)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Quản đốc không tồn tại.");
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Không có lệnh chẩn đoán để override.");
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.NotFound("Không tìm thấy lệnh sửa chữa.");
            }

            var resolvedTeamId = request.TeamId ?? await _diagnosisService.ResolveTeamIdForTechnicianAsync(request.TechnicianId);
            var primaryError = await ValidatePrimaryAndAssistantTechniciansAsync(
                _diagnosisService,
                request.TechnicianId,
                request.TeamId,
                resolvedTeamId,
                request.AssistantTechnicianId,
                teamMismatchMessage: "Kỹ thuật viên không thuộc tổ được chỉ định.");
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
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Khi có tổ phối hợp, bắt buộc chỉ định tổ chính.");
            }

            if (normalizedSupportingTeamIds.Count > 0)
            {
                var supportingError = await ValidateSupportingAssignmentsWithTechniciansAsync(
                    _diagnosisService,
                    normalizedSupportingAssignments,
                    serviceOrder.ExecuteBranchId);
                if (supportingError != null)
                {
                    return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest(supportingError);
                }
            }

            if (request.BayId.HasValue)
            {
                var bayInBranch = await _diagnosisService.IsBayInBranchAsync(request.BayId.Value, serviceOrder.ExecuteBranchId);
                if (!bayInBranch)
                {
                    return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Cầu nâng không thuộc chi nhánh xử lý.");
                }
            }

            await _diagnosisService.AssignDiagnosesAsync(
                activeDiagnosis,
                request.TechnicianId,
                assistantTechnicianId: request.AssistantTechnicianId,
                teamId: resolvedTeamId,
                supportingTeamIds: normalizedSupportingTeamIds,
                supportingAssignments: normalizedSupportingAssignments,
                bayId: request.BayId,
                managerOverride: true);

            var latest = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();
            var dto = await _responseService.BuildSingleResponseDtoAsync(latest);
            return DiagnosisCommandResult<DiagnosisResponseDto>.Ok(dto);
        }

        public async Task<DiagnosisCommandResult<DiagnosisResponseDto>> SelectSupportingTeamAssignmentAsync(
            long serviceOrderId,
            DiagnosisSupportingTeamSelectionRequestDto? request)
        {
            if (request == null || request.TeamLeaderId <= 0 || request.TeamId <= 0 || request.TechnicianId <= 0)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thiếu thông tin chọn thợ hỗ trợ của tổ phối hợp.");
            }

            if (request.AssistantTechnicianId.HasValue && request.AssistantTechnicianId.Value == request.TechnicianId)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thợ phụ không được trùng với thợ chính.");
            }

            var teamLeader = await _diagnosisService.GetStaffByIdAsync(request.TeamLeaderId);
            if (teamLeader == null)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Tổ trưởng không tồn tại.");
            }

            if (!teamLeader.TeamId.HasValue || teamLeader.TeamId.Value != request.TeamId)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Tổ trưởng chỉ được chọn nhân sự cho tổ của mình.");
            }

            var leadExists = await _diagnosisService.TechnicianExistsAsync(request.TechnicianId);
            if (!leadExists)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thợ chính không tồn tại.");
            }

            var leadInTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.TechnicianId, request.TeamId);
            if (!leadInTeam)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thợ chính không thuộc tổ được yêu cầu.");
            }

            if (request.AssistantTechnicianId.HasValue)
            {
                var assistantExists = await _diagnosisService.TechnicianExistsAsync(request.AssistantTechnicianId.Value);
                if (!assistantExists)
                {
                    return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thợ phụ không tồn tại.");
                }

                var assistantInTeam = await _diagnosisService.IsTechnicianInTeamAsync(request.AssistantTechnicianId.Value, request.TeamId);
                if (!assistantInTeam)
                {
                    return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Thợ phụ không thuộc tổ được yêu cầu.");
                }
            }

            var activeDiagnosis = await _diagnosisService.GetActiveDiagnosesAsync(serviceOrderId);
            if (activeDiagnosis.Count == 0)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest("Không có lệnh chẩn đoán đang hoạt động để cập nhật nhân sự hỗ trợ.");
            }

            try
            {
                await _diagnosisService.AssignSupportingTeamSelectionAsync(
                    activeDiagnosis,
                    request.TeamId,
                    request.TeamLeaderId,
                    request.TechnicianId,
                    request.AssistantTechnicianId,
                    request.Note);
            }
            catch (InvalidOperationException ex)
            {
                return DiagnosisCommandResult<DiagnosisResponseDto>.BadRequest(ex.Message);
            }

            var latest = activeDiagnosis
                .OrderByDescending(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .First();
            var dto = await _responseService.BuildSingleResponseDtoAsync(latest);
            return DiagnosisCommandResult<DiagnosisResponseDto>.Ok(dto);
        }
    }
}
