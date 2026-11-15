using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public sealed record QuotationCommandResult(int StatusCode, object? Payload = null)
    {
        public static QuotationCommandResult Ok(object? payload = null) => new(200, payload);
        public static QuotationCommandResult BadRequest(string message) => new(400, new { message });
        public static QuotationCommandResult Unauthorized() => new(401, null);
        public static QuotationCommandResult Forbid() => new(403, null);
        public static QuotationCommandResult NotFound(string? message = null) => new(404, message == null ? null : new { message });
    }

    public sealed record QuotationCommandResult<T>(int StatusCode, T? Data = default, object? ErrorPayload = null)
    {
        public static QuotationCommandResult<T> Ok(T data) => new(200, data, null);
        public static QuotationCommandResult<T> BadRequest(string message) => new(400, default, new { message });
        public static QuotationCommandResult<T> Unauthorized() => new(401, default, null);
        public static QuotationCommandResult<T> Forbid() => new(403, default, null);
        public static QuotationCommandResult<T> NotFound(string? message = null) => new(404, default, message == null ? null : new { message });
    }

    public interface IQuotationCommandFacade
    {
        Task<QuotationCommandResult<IReadOnlyList<QuotationDiscountResponseDto>>> UpsertDiscountsAsync(
            long quotationId,
            QuotationDiscountUpsertRequestDto? request,
            UserActorContext actor);

        Task<QuotationCommandResult<QuotationResponseDto>> FinancialApproveAsync(
            long quotationId,
            QuotationFinancialApprovalRequestDto? request,
            long? currentUserId);

        Task<QuotationCommandResult<BranchQuotationApprovalPolicyResponseDto>> UpsertBranchApprovalPolicyAsync(
            long branchId,
            BranchQuotationApprovalPolicyUpsertRequestDto? request,
            long? currentUserId);

        Task<QuotationCommandResult<QuotationResponseDto>> ApplyCustomerDecisionAsync(
            long quotationId,
            QuotationCustomerDecisionRequestDto? request,
            long? currentUserId,
            bool isStaffUser,
            CancellationToken cancellationToken);

        Task<QuotationCommandResult<QuotationResponseDto>> SubmitInternalApprovalAsync(
            long quotationId,
            QuotationStatusUpdateRequestDto? request,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist);

        Task<QuotationCommandResult<QuotationResponseDto>> SubmitAsync(
            long quotationId,
            QuotationStatusUpdateRequestDto? request,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist);

        Task<QuotationCommandResult<QuotationResponseDto>> ConfirmInsuranceApprovalAsync(
            long quotationId,
            QuotationInsuranceApprovalRequestDto? request,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist);

        Task<QuotationCommandResult<object>> UploadInsuranceEvidenceAsync(
            long quotationId,
            string originalFileName,
            string? contentType,
            byte[] fileBytes,
            CancellationToken cancellationToken);

        Task<QuotationCommandResult> DeleteInsuranceEvidenceAsync(
            long quotationId,
            CancellationToken cancellationToken);

        Task<QuotationCommandResult<QuotationResponseDto>> SendToCustomerAsync(
            long quotationId,
            QuotationStatusUpdateRequestDto? request,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist);

        Task<QuotationCommandResult<QuotationCustomerLinkResponseDto>> PublishCustomerLinkAsync(
            long quotationId,
            QuotationCustomerLinkPublishRequestDto? request,
            long? currentUserId,
            bool isServiceAdvisorOrReceptionist);

        Task<QuotationCommandResult<QuotationExcelExportResult>> ExportExcelAsync(
            long quotationId,
            CancellationToken cancellationToken);

        Task<QuotationCommandResult<QuotationResponseDto>> ImportExcelAsync(
            long quotationId,
            string originalFileName,
            byte[] fileBytes,
            long? actorStaffId,
            long? currentUserId,
            string? note,
            CancellationToken cancellationToken);
    }
}
