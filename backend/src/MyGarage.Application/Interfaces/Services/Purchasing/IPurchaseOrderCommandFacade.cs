using System;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;

namespace MyGarage.Interfaces
{
    public sealed record PurchaseOrderCommandResult<T>(int StatusCode, T? Data = default, object? ErrorPayload = null)
    {
        public static PurchaseOrderCommandResult<T> Ok(T data) => new(200, data, null);
        public static PurchaseOrderCommandResult<T> Created(T data) => new(201, data, null);
        public static PurchaseOrderCommandResult<T> BadRequest(string message) => new(400, default, new { message });
        public static PurchaseOrderCommandResult<T> Forbid() => new(403, default, null);
        public static PurchaseOrderCommandResult<T> NotFound(string? message = null) =>
            new(404, default, message == null ? null : new { message });
    }

    public sealed record PurchaseOrderCreatedPayload(long Id, object Payload);

    public interface IPurchaseOrderCommandFacade
    {
        Task<PurchaseOrderCommandResult<PagedEnvelope<object>>> GetAllAsync(
            long? scopedBranchId,
            string? status,
            int? page,
            int? pageSize,
            bool envelope,
            string? statusGroup,
            UserActorContext actor);

        Task<PurchaseOrderCommandResult<object>> GetByIdAsync(
            long id,
            long? scopedBranchId,
            UserActorContext actor);

        Task<PurchaseOrderCommandResult<PurchaseOrderCreatedPayload>> CreateAsync(
            PurchaseOrderCreateDto itemDto,
            long targetBranchId,
            UserActorContext actor);

        Task<PurchaseOrderCommandResult<object>> UpdateAsync(
            long id,
            PurchaseOrderUpdateDto? itemDto,
            UserActorContext actor);

        Task<PurchaseOrderCommandResult<object>> ApproveAsync(
            long id,
            long? approvedBy,
            string? note,
            UserActorContext actor);

        Task<PurchaseOrderCommandResult<object>> ReceiveAsync(
            long id,
            PurchaseOrderReceiveRequestDto? request,
            UserActorContext actor);

        Task<PurchaseOrderCommandResult<object>> GetReceiveHistoryAsync(
            long id,
            UserActorContext actor);

        Task<PurchaseOrderCommandResult<object>> GetFulfillmentAsync(
            long id,
            UserActorContext actor);

        Task<PurchaseOrderCommandResult<object>> GetFulfillmentDashboardAsync(
            long? scopedBranchId,
            DateOnly? fromDate,
            DateOnly? toDate,
            int page,
            int pageSize,
            UserActorContext actor);
    }
}
