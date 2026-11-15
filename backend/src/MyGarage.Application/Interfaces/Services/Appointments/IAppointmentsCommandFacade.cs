using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public sealed record AppointmentsCommandResult(int StatusCode, object? Payload = null)
    {
        public static AppointmentsCommandResult Ok(object? payload = null) => new(200, payload);
        public static AppointmentsCommandResult BadRequest(string message) => new(400, new { message });
        public static AppointmentsCommandResult Unauthorized() => new(401, null);
        public static AppointmentsCommandResult Forbid() => new(403, null);
        public static AppointmentsCommandResult NotFound(string? message = null) => new(404, message == null ? null : new { message });
    }

    public sealed record AppointmentsCommandResult<T>(int StatusCode, T? Data = default, object? ErrorPayload = null)
    {
        public static AppointmentsCommandResult<T> Ok(T data) => new(200, data, null);
        public static AppointmentsCommandResult<T> Created(T data) => new(201, data, null);
        public static AppointmentsCommandResult<T> BadRequest(string message) => new(400, default, new { message });
        public static AppointmentsCommandResult<T> Unauthorized() => new(401, default, null);
        public static AppointmentsCommandResult<T> Forbid() => new(403, default, null);
        public static AppointmentsCommandResult<T> NotFound(string? message = null) => new(404, default, message == null ? null : new { message });
    }

    public sealed record AppointmentActorContext(
        long? CurrentUserId,
        bool IsStaffUser,
        bool IsAdminStaff,
        long? CurrentBranchId,
        string? ClaimRole,
        string? ClaimRoleCode,
        string? ClaimRoleName);

    public sealed record AppointmentCreatedPayload(long AppointmentId, object? Detail);

    public sealed record AppointmentEditRequestItemDto
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

    public interface IAppointmentsCommandFacade
    {
        Task<PagedEnvelope<object>> GetPagedAsync(
            long? branchId,
            long? customerId,
            string? status,
            int? page,
            int? pageSize,
            AppointmentActorContext actor,
            bool forceCustomerShape = false);

        Task<AppointmentsCommandResult<object?>> GetByIdAsync(
            long id,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default);

        Task<AppointmentsCommandResult<AppointmentCreatedPayload>> CreateAsync(
            long actorUserId,
            BookAppointmentRequestDto dto,
            AppointmentActorContext actor);

        Task<AppointmentsCommandResult<AppointmentCreatedPayload>> CreateGuestAsync(
            BookAppointmentRequestDto dto);

        Task<AppointmentsCommandResult<ReceptionAppointmentSaveResponseDto>> SaveReceptionBookingAsync(
            ReceptionAppointmentSaveRequestDto? dto,
            AppointmentActorContext actor);

        Task<AppointmentsCommandResult<object>> UploadInsuranceAssetAsync(
            string fileName,
            string contentType,
            byte[] fileBytes,
            string? assetType,
            CancellationToken cancellationToken = default);

        Task<AppointmentsCommandResult<object?>> UpdateStatusAsync(
            long id,
            string? status,
            string? reason,
            DateTime? arrivedAt,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default);

        Task<AppointmentsCommandResult<AppointmentEditCapabilityResponseDto>> GetEditCapabilitiesAsync(
            long id,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default);

        Task<AppointmentsCommandResult<IReadOnlyList<AppointmentEditRequestItemDto>>> GetEditRequestsAsync(
            long id,
            string? status,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default);

        Task<AppointmentsCommandResult> RequestEditAsync(
            long id,
            EditRequestCreateDto? dto,
            AppointmentActorContext actor);

        Task<AppointmentsCommandResult> DecideEditRequestAsync(
            long id,
            long requestId,
            EditRequestDecisionDto? dto,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default);
    }
}
