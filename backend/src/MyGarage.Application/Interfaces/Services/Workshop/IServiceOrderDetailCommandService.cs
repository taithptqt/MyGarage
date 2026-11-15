using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public enum ServiceOrderDetailCommandStatus
    {
        Ok = 0,
        NoContent = 1,
        NotFound = 2,
        BadRequest = 3
    }

    public sealed record ServiceOrderDetailCommandResult(
        ServiceOrderDetailCommandStatus Status,
        string? ErrorMessage = null)
    {
        public static ServiceOrderDetailCommandResult NoContent() => new(ServiceOrderDetailCommandStatus.NoContent);
        public static ServiceOrderDetailCommandResult NotFound() => new(ServiceOrderDetailCommandStatus.NotFound);
        public static ServiceOrderDetailCommandResult BadRequest(string errorMessage) => new(ServiceOrderDetailCommandStatus.BadRequest, errorMessage);
    }

    public sealed record ServiceOrderDetailCommandResult<T>(
        ServiceOrderDetailCommandStatus Status,
        T? Data = default,
        string? ErrorMessage = null)
    {
        public static ServiceOrderDetailCommandResult<T> Ok(T data) => new(ServiceOrderDetailCommandStatus.Ok, data);
        public static ServiceOrderDetailCommandResult<T> NotFound() => new(ServiceOrderDetailCommandStatus.NotFound);
        public static ServiceOrderDetailCommandResult<T> BadRequest(string errorMessage) => new(ServiceOrderDetailCommandStatus.BadRequest, default, errorMessage);
    }

    public interface IServiceOrderDetailCommandService
    {
        Task<IReadOnlyList<ServiceOrderDetailResponseDto>> GetAllAsync(long? serviceOrderId = null, CancellationToken ct = default);
        Task<ServiceOrderDetailResponseDto?> GetByIdAsync(long id, CancellationToken ct = default);
        Task<ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>> CreateAsync(ServiceOrderDetailCreateDto? itemDto, CancellationToken ct = default);
        Task<ServiceOrderDetailCommandResult<ServiceOrderDetailResponseDto>> UpdateAsync(long id, ServiceOrderDetailUpdateDto? itemDto, CancellationToken ct = default);
        Task<ServiceOrderDetailCommandResult> DeleteAsync(long id, CancellationToken ct = default);
    }
}
