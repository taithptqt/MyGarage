using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public partial class QuotationCommandFacade : IQuotationCommandFacade
    {
        private readonly IQuotationWorkflowService _quotationWorkflowService;
        private readonly ICloudStorageService _cloudStorageService;
        private readonly IQuotationDataService _dataService;
        private readonly IQuotationAuthorizationService _quotationAuthorizationService;
        private readonly IQuotationExcelService _quotationExcelService;

        public QuotationCommandFacade(
            IQuotationWorkflowService quotationWorkflowService,
            ICloudStorageService cloudStorageService,
            IQuotationDataService dataService,
            IQuotationAuthorizationService quotationAuthorizationService,
            IQuotationExcelService quotationExcelService)
        {
            _quotationWorkflowService = quotationWorkflowService;
            _cloudStorageService = cloudStorageService;
            _dataService = dataService;
            _quotationAuthorizationService = quotationAuthorizationService;
            _quotationExcelService = quotationExcelService;
        }

        public async Task<QuotationCommandResult<IReadOnlyList<QuotationDiscountResponseDto>>> UpsertDiscountsAsync(
            long quotationId,
            QuotationDiscountUpsertRequestDto? request,
            UserActorContext actor)
        {
            try
            {
                if (!await _quotationAuthorizationService.CanUpdateQuotationDiscountsAsync(quotationId, actor))
                {
                    return QuotationCommandResult<IReadOnlyList<QuotationDiscountResponseDto>>.Forbid();
                }

                var normalizedRequest = request ?? new QuotationDiscountUpsertRequestDto();
                var actorStaffId = ResolveActorStaffId(normalizedRequest.ActorStaffId, actor.UserId);
                if (!actorStaffId.HasValue)
                {
                    return QuotationCommandResult<IReadOnlyList<QuotationDiscountResponseDto>>.BadRequest("Thiếu actorStaffId hợp lệ.");
                }

                if (!actor.IsAdmin && actor.UserId.HasValue && actorStaffId.Value != actor.UserId.Value)
                {
                    return QuotationCommandResult<IReadOnlyList<QuotationDiscountResponseDto>>.Forbid();
                }

                normalizedRequest.ActorStaffId = actorStaffId.Value;
                var rows = await _quotationWorkflowService.UpsertQuotationDiscountsAsync(quotationId, normalizedRequest);
                return QuotationCommandResult<IReadOnlyList<QuotationDiscountResponseDto>>.Ok(rows);
            }
            catch (InvalidOperationException ex)
            {
                return QuotationCommandResult<IReadOnlyList<QuotationDiscountResponseDto>>.BadRequest(ex.Message);
            }
        }

        public async Task<QuotationCommandResult<QuotationResponseDto>> FinancialApproveAsync(
            long quotationId,
            QuotationFinancialApprovalRequestDto? request,
            long? currentUserId)
        {
            try
            {
                var normalizedRequest = request ?? new QuotationFinancialApprovalRequestDto();
                if (normalizedRequest.ApprovedBy <= 0)
                {
                    if (!currentUserId.HasValue || currentUserId.Value <= 0)
                    {
                        return QuotationCommandResult<QuotationResponseDto>.BadRequest("Thiếu ApprovedBy hợp lệ.");
                    }

                    normalizedRequest.ApprovedBy = currentUserId.Value;
                }

                var updated = await _quotationWorkflowService.FinancialApproveQuotationAsync(quotationId, normalizedRequest);
                return QuotationCommandResult<QuotationResponseDto>.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return QuotationCommandResult<QuotationResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<QuotationCommandResult<BranchQuotationApprovalPolicyResponseDto>> UpsertBranchApprovalPolicyAsync(
            long branchId,
            BranchQuotationApprovalPolicyUpsertRequestDto? request,
            long? currentUserId)
        {
            try
            {
                var normalizedRequest = request ?? new BranchQuotationApprovalPolicyUpsertRequestDto();
                if (normalizedRequest.ActorStaffId <= 0)
                {
                    if (!currentUserId.HasValue || currentUserId.Value <= 0)
                    {
                        return QuotationCommandResult<BranchQuotationApprovalPolicyResponseDto>.BadRequest("Thiếu ActorStaffId hợp lệ.");
                    }

                    normalizedRequest.ActorStaffId = currentUserId.Value;
                }

                var updated = await _quotationWorkflowService.UpsertBranchQuotationApprovalPolicyAsync(branchId, normalizedRequest);
                return QuotationCommandResult<BranchQuotationApprovalPolicyResponseDto>.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return QuotationCommandResult<BranchQuotationApprovalPolicyResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<QuotationCommandResult<QuotationResponseDto>> ApplyCustomerDecisionAsync(
            long quotationId,
            QuotationCustomerDecisionRequestDto? request,
            long? currentUserId,
            bool isStaffUser,
            CancellationToken cancellationToken)
        {
            try
            {
                var normalizedRequest = request ?? new QuotationCustomerDecisionRequestDto();
                if (!currentUserId.HasValue || currentUserId.Value <= 0)
                {
                    return QuotationCommandResult<QuotationResponseDto>.Unauthorized();
                }

                if (isStaffUser)
                {
                    normalizedRequest.ActorStaffId = currentUserId.Value;
                    normalizedRequest.CustomerId = null;
                }
                else
                {
                    var ownerCustomerId = await _dataService.GetOwnerCustomerIdAsync(quotationId, cancellationToken);
                    if (!ownerCustomerId.HasValue || ownerCustomerId.Value != currentUserId.Value)
                    {
                        return QuotationCommandResult<QuotationResponseDto>.Forbid();
                    }

                    normalizedRequest.ActorStaffId = null;
                    normalizedRequest.CustomerId = currentUserId.Value;
                }

                var updated = await _quotationWorkflowService.ApplyCustomerDecisionAsync(quotationId, normalizedRequest);
                return QuotationCommandResult<QuotationResponseDto>.Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return QuotationCommandResult<QuotationResponseDto>.BadRequest(ex.Message);
            }
        }

        public async Task<QuotationCommandResult<QuotationResponseDto>> SubmitInternalApprovalAsync(
            long quotationId,
            QuotationStatusUpdateRequestDto? request,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist)
        {
            return await ExecuteAdvisorActionAsync(
                request?.ActorStaffId,
                currentUserId,
                isServiceAdvisorOrReceptionist,
                actorStaffId => _quotationWorkflowService.SubmitQuotationForInternalApprovalAsync(quotationId, actorStaffId, request?.Note));
        }

        public async Task<QuotationCommandResult<QuotationResponseDto>> SubmitAsync(
            long quotationId,
            QuotationStatusUpdateRequestDto? request,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist)
        {
            return await ExecuteAdvisorActionAsync(
                request?.ActorStaffId,
                currentUserId,
                isServiceAdvisorOrReceptionist,
                actorStaffId => _quotationWorkflowService.SubmitQuotationAsync(quotationId, actorStaffId, request?.Note));
        }

        public async Task<QuotationCommandResult<QuotationResponseDto>> ConfirmInsuranceApprovalAsync(
            long quotationId,
            QuotationInsuranceApprovalRequestDto? request,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist)
        {
            var normalizedRequest = request ?? new QuotationInsuranceApprovalRequestDto();
            return await ExecuteAdvisorActionAsync(
                normalizedRequest.ActorStaffId,
                currentUserId,
                isServiceAdvisorOrReceptionist,
                actorStaffId =>
                {
                    normalizedRequest.ActorStaffId = actorStaffId;
                    return _quotationWorkflowService.ConfirmInsuranceApprovedAsync(quotationId, normalizedRequest);
                });
        }

        public async Task<QuotationCommandResult<object>> UploadInsuranceEvidenceAsync(
            long quotationId,
            string originalFileName,
            string? contentType,
            byte[] fileBytes,
            CancellationToken cancellationToken)
        {
            if (quotationId <= 0)
            {
                return QuotationCommandResult<object>.BadRequest("Mã báo giá không hợp lệ.");
            }

            if (!await _dataService.QuotationExistsAsync(quotationId, cancellationToken))
            {
                return QuotationCommandResult<object>.NotFound("Không tìm thấy báo giá.");
            }

            var extension = Path.GetExtension(originalFileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".bin";
            }

            var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType;
            var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";

            await using var stream = new MemoryStream(fileBytes);
            var evidenceUrl = await _cloudStorageService.UploadAsync(
                stream,
                $"quotations/{quotationId}/insurance-evidence",
                fileName,
                resolvedContentType,
                cancellationToken);

            if (!await _dataService.SetInsuranceApprovalEvidenceUrlAsync(quotationId, evidenceUrl, cancellationToken))
            {
                return QuotationCommandResult<object>.NotFound("Không tìm thấy báo giá.");
            }

            return QuotationCommandResult<object>.Ok(new { quotationId, insuranceApprovalEvidenceUrl = evidenceUrl });
        }

        public async Task<QuotationCommandResult> DeleteInsuranceEvidenceAsync(
            long quotationId,
            CancellationToken cancellationToken)
        {
            if (quotationId <= 0)
            {
                return QuotationCommandResult.BadRequest("Mã báo giá không hợp lệ.");
            }

            if (!await _dataService.SetInsuranceApprovalEvidenceUrlAsync(quotationId, null, cancellationToken))
            {
                return QuotationCommandResult.NotFound("Không tìm thấy báo giá.");
            }

            return QuotationCommandResult.Ok(new { message = "Đã xóa bằng chứng bảo hiểm thành công." });
        }

        public async Task<QuotationCommandResult<QuotationResponseDto>> SendToCustomerAsync(
            long quotationId,
            QuotationStatusUpdateRequestDto? request,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist)
        {
            return await ExecuteAdvisorActionAsync(
                request?.ActorStaffId,
                currentUserId,
                isServiceAdvisorOrReceptionist,
                actorStaffId => _quotationWorkflowService.SendQuotationToCustomerAsync(
                    quotationId,
                    actorStaffId,
                    request?.Note,
                    request?.IdempotencyKey));
        }

        public async Task<QuotationCommandResult<QuotationCustomerLinkResponseDto>> PublishCustomerLinkAsync(
            long quotationId,
            QuotationCustomerLinkPublishRequestDto? request,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist)
        {
            var normalizedRequest = request ?? new QuotationCustomerLinkPublishRequestDto();
            return await ExecuteAdvisorActionAsync(
                normalizedRequest.ActorStaffId,
                currentUserId,
                isServiceAdvisorOrReceptionist,
                actorStaffId =>
                {
                    normalizedRequest.ActorStaffId = actorStaffId;
                    return _quotationWorkflowService.PublishQuotationCustomerLinkAsync(quotationId, normalizedRequest);
                });
        }

        private static async Task<QuotationCommandResult<T>> ExecuteAdvisorActionAsync<T>(
            long? requestActorStaffId,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist,
            Func<long, Task<T>> action)
        {
            try
            {
                if (!isServiceAdvisorOrReceptionist)
                {
                    return QuotationCommandResult<T>.Forbid();
                }

                var actorStaffId = ResolveActorStaffId(requestActorStaffId, currentUserId);
                if (!actorStaffId.HasValue)
                {
                    return QuotationCommandResult<T>.BadRequest("Thiếu actorStaffId hợp lệ.");
                }

                var data = await action(actorStaffId.Value);
                return QuotationCommandResult<T>.Ok(data);
            }
            catch (InvalidOperationException ex)
            {
                return QuotationCommandResult<T>.BadRequest(ex.Message);
            }
        }

        private static long? ResolveActorStaffId(long? requestActorStaffId, long? currentUserId)
        {
            if (requestActorStaffId.HasValue && requestActorStaffId.Value > 0)
            {
                return requestActorStaffId.Value;
            }

            return currentUserId;
        }
    }
}
