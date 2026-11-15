using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public sealed record ServiceOrderCommandResult(int StatusCode, object? Payload = null)
    {
        public static ServiceOrderCommandResult Ok(object? payload = null) => new(200, payload);
        public static ServiceOrderCommandResult NoContent() => new(204, null);
        public static ServiceOrderCommandResult BadRequest(string message) => new(400, new { message });
        public static ServiceOrderCommandResult Unauthorized() => new(401, null);
        public static ServiceOrderCommandResult Forbid() => new(403, null);
        public static ServiceOrderCommandResult NotFound(string? message = null) => new(404, message == null ? null : new { message });
    }

    public sealed record ServiceOrderCommandResult<T>(int StatusCode, T? Data = default, object? ErrorPayload = null)
    {
        public static ServiceOrderCommandResult<T> Ok(T data) => new(200, data, null);
        public static ServiceOrderCommandResult<T> Created(T data) => new(201, data, null);
        public static ServiceOrderCommandResult<T> BadRequest(string message) => new(400, default, new { message });
        public static ServiceOrderCommandResult<T> Unauthorized() => new(401, default, null);
        public static ServiceOrderCommandResult<T> Forbid() => new(403, default, null);
        public static ServiceOrderCommandResult<T> NotFound(string? message = null) => new(404, default, message == null ? null : new { message });
    }

    public sealed record ServiceOrderEditRequestItemDto
    {
        public long RequestId { get; init; }
        public long? UserId { get; init; }
        public long? BranchId { get; init; }
        public DateTime Timestamp { get; init; }
        public string? Section { get; init; }
        public string? Reason { get; init; }
        public string? Status { get; init; }
        public long? RequestedByUserId { get; init; }
        public DateTime? RequestedAtUtc { get; init; }
        public long? DecidedByUserId { get; init; }
        public DateTime? DecidedAtUtc { get; init; }
        public string? DecisionNote { get; init; }
        public string? DecidedByRoleCode { get; init; }
        public string? DecidedByRoleName { get; init; }
        public DateTime? OverrideUntilUtc { get; init; }
    }

    public interface IServiceOrderCommandFacade
    {
        Task<PagedEnvelope<ServiceOrderResponseDto>> GetPagedAsync(
            long? scopedBranchId,
            string? status,
            int page,
            int pageSize);

        Task<ServiceOrderResponseDto?> GetByIdAsync(long id);

        Task<ServiceOrderCommandResult<ServiceOrderResponseDto>> CreateAsync(
            ServiceOrderCreateDto itemDto,
            long? actorUserId);

        Task<ServiceOrderCommandResult<ServiceOrderBatchCreateResponseDto>> CreateBatchAsync(
            ServiceOrderBatchCreateRequestDto dto,
            long? actorUserId);

        Task<ServiceOrderCommandResult<ServiceOrderResponseDto>> UpdateWorkshopAsync(
            long id,
            WorkshopServiceOrderEditRequestDto dto,
            long? actorUserId);

        Task<ServiceOrderCommandResult<ServiceOrderResponseDto>> UpdateAsync(
            long id,
            ServiceOrderUpdateDto itemDto,
            long? actorUserId);

        Task<ServiceOrderCommandResult> DeleteAsync(long id);

        Task<ServiceOrderCommandResult<object>> UploadInsuranceAssetAsync(
            string fileName,
            string contentType,
            byte[] fileBytes,
            string? assetType,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<ServiceOrderEditRequestItemDto>> GetEditRequestsAsync(long id, string? status);

        Task<ServiceOrderCommandResult> RequestEditAsync(
            long id,
            long? currentUserId,
            EditRequestCreateDto? dto);

        Task<ServiceOrderCommandResult> DecideEditRequestAsync(
            long id,
            long requestId,
            long? currentUserId,
            string? claimRoleCode,
            string? claimRoleName,
            EditRequestDecisionDto? dto);
    }
}
