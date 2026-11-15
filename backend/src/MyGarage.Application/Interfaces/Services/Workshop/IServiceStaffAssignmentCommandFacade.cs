using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public sealed record ServiceStaffAssignmentCommandResult(int StatusCode, object? Payload = null)
    {
        public static ServiceStaffAssignmentCommandResult Ok(object? payload = null) => new(200, payload);
        public static ServiceStaffAssignmentCommandResult NoContent() => new(204, null);
        public static ServiceStaffAssignmentCommandResult BadRequest(string message) => new(400, new { message });
        public static ServiceStaffAssignmentCommandResult BadRequest(object payload) => new(400, payload);
        public static ServiceStaffAssignmentCommandResult Forbid() => new(403, null);
        public static ServiceStaffAssignmentCommandResult NotFound(string? message = null) => new(404, message == null ? null : new { message });
    }

    public sealed record ServiceStaffAssignmentCommandResult<T>(int StatusCode, T? Data = default, object? ErrorPayload = null)
    {
        public static ServiceStaffAssignmentCommandResult<T> Ok(T data) => new(200, data, null);
        public static ServiceStaffAssignmentCommandResult<T> Created(T data) => new(201, data, null);
        public static ServiceStaffAssignmentCommandResult<T> BadRequest(string message) => new(400, default, new { message });
        public static ServiceStaffAssignmentCommandResult<T> BadRequest(object payload) => new(400, default, payload);
        public static ServiceStaffAssignmentCommandResult<T> Forbid() => new(403, default, null);
        public static ServiceStaffAssignmentCommandResult<T> NotFound(string? message = null) => new(404, default, message == null ? null : new { message });
    }

    public sealed record ServiceStaffAssignmentBulkItemInput
    {
        public long? ServiceOrderId { get; init; }
        public long ServiceOrderDetailId { get; init; }
        public long? StaffId { get; init; }
        public long? TechnicianId { get; init; }
        public string? RoleInTask { get; init; }
        public decimal? SplitRatio { get; init; }
        public decimal? EstimatedCommission { get; init; }
        public string? Status { get; init; }
        public bool RequiresInsuranceProof { get; init; }
        public string? ProofImageUrl { get; init; }
        public bool? IsRework { get; init; }
        public long? AssignedBy { get; init; }
    }

    public sealed record ServiceStaffAssignmentLifecycleInput(
        DateTime? StartedAt,
        DateTime? CompletedAt,
        string? Note,
        string? ProofImageUrl);

    public sealed record ServiceStaffAssignmentBulkResponse(
        int Count,
        IReadOnlyList<ServiceStaffAssignmentResponseDto> Items);

    public interface IServiceStaffAssignmentCommandFacade
    {
        Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> CreateAsync(
            ServiceStaffAssignmentCreateDto itemDto,
            long? scopedBranchId,
            long? currentUserId);

        Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> UpdateAsync(
            long id,
            ServiceStaffAssignmentUpdateDto itemDto,
            long? scopedBranchId);

        Task<ServiceStaffAssignmentCommandResult> DeleteAsync(
            long id,
            long? scopedBranchId);

        Task<ServiceStaffAssignmentCommandResult<IEnumerable<ServiceStaffAssignmentResponseDto>>> AssignMultiAsync(
            ServiceStaffAssignmentMultiCreateDto request,
            long? scopedBranchId,
            long? currentUserId);

        Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentBulkResponse>> BulkAssignAsync(
            IReadOnlyList<ServiceStaffAssignmentBulkItemInput>? assignments,
            long? scopedBranchId,
            long? currentUserId);

        Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> StartAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman);

        Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> PauseAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman);

        Task<ServiceStaffAssignmentCommandResult<ServiceStaffAssignmentResponseDto>> CompleteAssignmentAsync(
            long id,
            ServiceStaffAssignmentLifecycleInput? request,
            long? scopedBranchId,
            long? currentUserId,
            bool isManagerOrForeman);
    }
}
