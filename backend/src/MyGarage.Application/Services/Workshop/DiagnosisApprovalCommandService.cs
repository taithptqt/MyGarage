using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Application.Interfaces.Stores.Workshop;
using static MyGarage.Services.DiagnosisCommandHelpers;

namespace MyGarage.Services
{
    public class DiagnosisApprovalCommandService : IDiagnosisApprovalCommandService
    {
        private readonly IDiagnosisService _diagnosisService;
        private readonly IQuotationBuildService _quotationBuildService;
        private readonly IQuotationQueryService _quotationQueryService;
        private readonly IQuotationBuildCommandStore _transactionStore;

        public DiagnosisApprovalCommandService(
            IDiagnosisService diagnosisService,
            IQuotationBuildService quotationBuildService,
            IQuotationQueryService quotationQueryService,
            IQuotationBuildCommandStore transactionStore)
        {
            _diagnosisService = diagnosisService;
            _quotationBuildService = quotationBuildService;
            _quotationQueryService = quotationQueryService;
            _transactionStore = transactionStore;
        }

        public async Task<DiagnosisCommandResult> ApproveTechnicalAsync(
            long serviceOrderId,
            DiagnosisTechnicalApprovalRequestDto? request)
        {
            if (request == null || request.TeamLeaderId <= 0)
            {
                return DiagnosisCommandResult.BadRequest("Thiếu thông tin tổ trưởng duyệt kỹ thuật.");
            }

            var teamLeader = await _diagnosisService.GetStaffByIdAsync(request.TeamLeaderId);
            if (teamLeader == null)
            {
                return DiagnosisCommandResult.BadRequest("Tổ trưởng không tồn tại.");
            }

            var diagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
                return DiagnosisCommandResult.BadRequest("Không có kết quả chẩn đoán để duyệt.");
            }

            var serviceOrder = await _diagnosisService.GetServiceOrderAsync(serviceOrderId);
            var executeBranchId = serviceOrder?.ExecuteBranchId;

            if (diagnoses.Any(x => !string.Equals(x.Status, DiagnosisStatusCompleted, StringComparison.OrdinalIgnoreCase)))
            {
                return DiagnosisCommandResult.BadRequest("Chỉ được duyệt kỹ thuật sau khi toàn bộ triệu chứng đã ở trạng thái COMPLETED.");
            }

            var isEscalatedToForeman = diagnoses.Any(x => _diagnosisService.IsTeamLeaderApprovalEscalated(x, executeBranchId));
            if (isEscalatedToForeman)
            {
                return DiagnosisCommandResult.BadRequest("Phiếu đã quá SLA duyệt tổ trưởng và đã chuyển lên quản đốc xử lý.");
            }

            if (diagnoses.Any(x => IsFinalDecisionMade(_diagnosisService.ReadFinalApprovalStatus(x))))
            {
                return DiagnosisCommandResult.BadRequest("Phiếu đã có quyết định duyệt cuối, không thể duyệt kỹ thuật lại.");
            }

            var technicalAlreadyApproved = diagnoses.All(x =>
                string.Equals(
                    _diagnosisService.ReadTeamLeaderApprovalStatus(x),
                    nameof(ApprovalDecisionStatusCode.APPROVED),
                    StringComparison.OrdinalIgnoreCase));
            if (technicalAlreadyApproved)
            {
                return DiagnosisCommandResult.Ok(new { message = "Tổ trưởng đã duyệt kỹ thuật trước đó." });
            }

            await _diagnosisService.ApproveTechnicalAsync(diagnoses, request.TeamLeaderId, request.Note);
            return DiagnosisCommandResult.Ok(new { message = "Đã duyệt báo cáo kỹ thuật." });
        }

        public async Task<DiagnosisCommandResult> RejectTechnicalAsync(
            long serviceOrderId,
            DiagnosisTechnicalApprovalRequestDto? request)
        {
            if (request == null || request.TeamLeaderId <= 0)
            {
                return DiagnosisCommandResult.BadRequest("Thiếu thông tin tổ trưởng từ chối.");
            }

            if (string.IsNullOrWhiteSpace(request.Note))
            {
                return DiagnosisCommandResult.BadRequest("Bắt buộc nhập lý do từ chối để yêu cầu thợ làm lại.");
            }

            var teamLeader = await _diagnosisService.GetStaffByIdAsync(request.TeamLeaderId);
            if (teamLeader == null)
            {
                return DiagnosisCommandResult.BadRequest("Tổ trưởng không tồn tại.");
            }

            var diagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
                return DiagnosisCommandResult.BadRequest("Không có kết quả chẩn đoán để xử lý.");
            }

            await _diagnosisService.RejectTechnicalAsync(diagnoses, request.TeamLeaderId, request.Note);
            return DiagnosisCommandResult.Ok(new { message = "Tổ trưởng đã từ chối và trả về cho thợ làm lại." });
        }

        public async Task<DiagnosisCommandResult> CloneRejectedVersionAsync(
            long serviceOrderId,
            DiagnosisCloneVersionRequestDto? request)
        {
            if (request == null || request.ActorId <= 0)
            {
                return DiagnosisCommandResult.BadRequest("Thiếu thông tin người thao tác clone phiên bản.");
            }

            var diagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
                return DiagnosisCommandResult.BadRequest("Không có phiếu chẩn đoán để clone phiên bản.");
            }

            var affected = await _diagnosisService.CloneRejectedDiagnosesVersionAsync(diagnoses, request.ActorId, request.Note);
            if (affected <= 0)
            {
                return DiagnosisCommandResult.BadRequest("Chỉ clone được khi phiên bản hiện tại đang bị từ chối duyệt.");
            }

            return DiagnosisCommandResult.Ok(new { message = "Đã tạo phiên bản mới để kỹ thuật viên chỉnh sửa lại.", affected });
        }

        public async Task<DiagnosisCommandResult> ApproveFinalAsync(long serviceOrderId, DiagnosisFinalApprovalRequestDto? request)
        {
            try
            {
                return await _transactionStore.ExecuteDiagnosisApprovalTransactionalAsync(serviceOrderId, () => ApproveFinalCoreAsync(serviceOrderId, request));
            }
            catch (InvalidOperationException ex) { return DiagnosisCommandResult.BadRequest(ex.Message); }
        }

        private async Task<DiagnosisCommandResult> ApproveFinalCoreAsync(
            long serviceOrderId,
            DiagnosisFinalApprovalRequestDto? request)
        {
            if (request == null || request.TeamLeaderId <= 0)
            {
                return DiagnosisCommandResult.BadRequest("Thiếu thông tin tổ trưởng duyệt báo giá.");
            }

            var teamLeader = await _diagnosisService.GetStaffByIdAsync(request.TeamLeaderId);
            if (teamLeader == null)
            {
                return DiagnosisCommandResult.BadRequest("Tổ trưởng không tồn tại.");
            }

            var diagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
                return DiagnosisCommandResult.BadRequest("Không có dữ liệu chẩn đoán để duyệt.");
            }

            if (diagnoses.Any(x => !_diagnosisService.ReadApprovedByForemanId(x).HasValue))
            {
                return DiagnosisCommandResult.BadRequest("Cần tổ trưởng duyệt báo cáo kỹ thuật trước khi duyệt báo giá.");
            }

            var validationMessage = await _quotationBuildService.ValidateInitialQuotationFromDiagnosisAsync(
                serviceOrderId, diagnoses.Select(x => x.Id).Distinct().ToList());
            if (!string.IsNullOrWhiteSpace(validationMessage))
                return DiagnosisCommandResult.BadRequest(validationMessage);

            await _diagnosisService.ApproveFinalAsync(diagnoses, request.TeamLeaderId, request.Note);
            var quotation = await _quotationBuildService.GenerateInitialQuotationFromDiagnosisAsync(
                serviceOrderId,
                request.TeamLeaderId,
                request.Note,
                diagnoses.Select(x => x.Id).Distinct().ToList());
            return DiagnosisCommandResult.Ok(new
            {
                message = "Đã duyệt chốt kỹ thuật và sinh báo giá V1.",
                quotation
            });
        }

        public async Task<DiagnosisCommandResult> ForemanDecisionAsync(long serviceOrderId, DiagnosisForemanDecisionRequestDto? request)
        {
            try
            {
                return await _transactionStore.ExecuteDiagnosisApprovalTransactionalAsync(serviceOrderId, () => ForemanDecisionCoreAsync(serviceOrderId, request));
            }
            catch (InvalidOperationException ex) { return DiagnosisCommandResult.BadRequest(ex.Message); }
        }

        private async Task<DiagnosisCommandResult> ForemanDecisionCoreAsync(
            long serviceOrderId,
            DiagnosisForemanDecisionRequestDto? request)
        {
            if (request == null || request.ForemanId <= 0)
            {
                return DiagnosisCommandResult.BadRequest("Thiếu thông tin quản đốc duyệt.");
            }

            var foreman = await _diagnosisService.GetStaffByIdAsync(request.ForemanId);
            if (foreman == null)
            {
                return DiagnosisCommandResult.BadRequest("Quản đốc không tồn tại.");
            }

            var diagnoses = await _diagnosisService.GetLatestWorkflowDiagnosesByServiceOrderAsync(serviceOrderId);
            if (diagnoses.Count == 0)
            {
                return DiagnosisCommandResult.BadRequest("Không có dữ liệu chẩn đoán để xử lý.");
            }

            if (diagnoses.Any(x => !string.Equals(x.Status, DiagnosisStatusCompleted, StringComparison.OrdinalIgnoreCase)))
            {
                return DiagnosisCommandResult.BadRequest("Chỉ được duyệt/từ chối sau khi thợ đã gửi kết quả chẩn đoán hoàn tất.");
            }

            var diagnosisIds = diagnoses.Select(x => x.Id).Distinct().ToList();
            var teamLeaderApproved = diagnoses.All(x =>
                string.Equals(
                    _diagnosisService.ReadTeamLeaderApprovalStatus(x),
                    nameof(ApprovalDecisionStatusCode.APPROVED),
                    StringComparison.OrdinalIgnoreCase));

            if (diagnoses.Any(x => IsFinalDecisionMade(_diagnosisService.ReadFinalApprovalStatus(x))))
            {
                return await HandleAlreadyDecidedForemanDecisionAsync(serviceOrderId, request, diagnoses, diagnosisIds);
            }

            if (request.Approve)
            {
                var validationMessage = await _quotationBuildService.ValidateInitialQuotationFromDiagnosisAsync(
                    serviceOrderId,
                    diagnosisIds);
                if (!string.IsNullOrWhiteSpace(validationMessage))
                {
                    return DiagnosisCommandResult.BadRequest(validationMessage);
                }

                var bypassTeamLeader = !teamLeaderApproved;
                var firstTechnicianId = diagnoses
                    .Select(_diagnosisService.ReadTechnicianId)
                    .Where(x => x.HasValue && x.Value > 0)
                    .Select(x => x!.Value)
                    .FirstOrDefault();
                var technician = firstTechnicianId > 0
                    ? await _diagnosisService.GetStaffByIdAsync(firstTechnicianId)
                    : null;
                var bypassAuditNote = bypassTeamLeader
                    ? $"Quản đốc {foreman.FullName} duyệt vượt cấp từ thợ {technician?.FullName ?? "N/A"}"
                    : null;

                await _diagnosisService.ApproveFinalAsync(diagnoses, request.ForemanId, request.Note, bypassTeamLeader, bypassAuditNote);

                var quotation = await _quotationBuildService.GenerateInitialQuotationFromDiagnosisAsync(serviceOrderId, request.ForemanId, request.Note, diagnosisIds);

                return DiagnosisCommandResult.Ok(new
                {
                    message = bypassTeamLeader
                        ? "Quản đốc đã duyệt vượt cấp và chuyển báo giá."
                        : "Quản đốc đã duyệt và chuyển báo giá.",
                    quotation
                });
            }

            await _diagnosisService.RejectFinalAsync(diagnoses, request.ForemanId, request.Note);
            return DiagnosisCommandResult.Ok(new { message = "Quản đốc từ chối, yêu cầu chẩn đoán lại." });
        }

        private async Task<DiagnosisCommandResult> HandleAlreadyDecidedForemanDecisionAsync(
            long serviceOrderId,
            DiagnosisForemanDecisionRequestDto request,
            IReadOnlyList<Models.Diagnosis> diagnoses,
            List<long> diagnosisIds)
        {
            if (!request.Approve)
            {
                return DiagnosisCommandResult.BadRequest("Phiếu đã có quyết định duyệt cuối.");
            }

            var hasRejectedFinalDecision = diagnoses.Any(x =>
                string.Equals(
                    _diagnosisService.ReadFinalApprovalStatus(x),
                    nameof(ApprovalDecisionStatusCode.REJECTED),
                    StringComparison.OrdinalIgnoreCase));
            if (hasRejectedFinalDecision)
            {
                return DiagnosisCommandResult.BadRequest("Phiếu đã bị từ chối duyệt cuối, không thể tự động tạo báo giá.");
            }

            var existingQuotations = await _quotationQueryService.GetQuotationsByServiceOrderAsync(serviceOrderId);
            if (existingQuotations.Count > 0)
            {
                var latestQuotation = existingQuotations
                    .OrderByDescending(x => x.VersionNo)
                    .ThenByDescending(x => x.Id)
                    .First();
                return DiagnosisCommandResult.Ok(new
                {
                    message = "Phiếu đã duyệt và đã có báo giá.",
                    quotation = latestQuotation
                });
            }

            var validationMessage = await _quotationBuildService.ValidateInitialQuotationFromDiagnosisAsync(
                serviceOrderId,
                diagnosisIds);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return DiagnosisCommandResult.BadRequest(validationMessage);
            }

            var quotation = await _quotationBuildService.GenerateInitialQuotationFromDiagnosisAsync(serviceOrderId, request.ForemanId, request.Note, diagnosisIds);

            return DiagnosisCommandResult.Ok(new
            {
                message = "Phiếu đã duyệt trước đó, hệ thống đã tạo bù báo giá.",
                quotation
            });
        }
    }
}
