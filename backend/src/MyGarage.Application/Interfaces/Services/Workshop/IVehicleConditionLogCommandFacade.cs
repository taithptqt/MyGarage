using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public sealed record VehicleConditionLogCommandResult<T>(int StatusCode, T? Data = default, object? ErrorPayload = null)
    {
        public static VehicleConditionLogCommandResult<T> Ok(T data) => new(200, data, null);
        public static VehicleConditionLogCommandResult<T> Created(T data) => new(201, data, null);
        public static VehicleConditionLogCommandResult<T> BadRequest(string message) => new(400, default, new { message });
        public static VehicleConditionLogCommandResult<T> NotFound(string? message = null) => new(404, default, message == null ? null : new { message });
    }

    public interface IVehicleConditionLogCommandFacade
    {
        Task<VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>> CreateRequestAsync(
            long serviceOrderId,
            string? requestType);

        Task<VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>> CreateAsync(
            VehicleConditionLogCreateDto? itemDto,
            CancellationToken cancellationToken);

        Task<VehicleConditionLogCommandResult<VehicleConditionLogResponseDto>> UpdateAsync(
            long id,
            VehicleConditionLogUpdateDto? itemDto,
            CancellationToken cancellationToken);

        Task<VehicleConditionLogCommandResult<object>> UploadMediaAsync(
            string originalFileName,
            string contentType,
            byte[] fileBytes,
            string folderName,
            CancellationToken cancellationToken);
    }
}
