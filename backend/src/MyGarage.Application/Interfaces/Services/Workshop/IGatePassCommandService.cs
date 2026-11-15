using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public enum GatePassCommandStatus
    {
        Ok = 0,
        NoContent = 1,
        NotFound = 2,
        BadRequest = 3
    }

    public sealed record GatePassCommandResult(
        GatePassCommandStatus Status,
        string? ErrorMessage = null)
    {
        public static GatePassCommandResult NoContent() => new(GatePassCommandStatus.NoContent);
        public static GatePassCommandResult NotFound() => new(GatePassCommandStatus.NotFound);
        public static GatePassCommandResult BadRequest(string errorMessage) => new(GatePassCommandStatus.BadRequest, errorMessage);
    }

    public sealed record GatePassCommandResult<T>(
        GatePassCommandStatus Status,
        T? Data = default,
        string? ErrorMessage = null)
    {
        public static GatePassCommandResult<T> Ok(T data) => new(GatePassCommandStatus.Ok, data);
        public static GatePassCommandResult<T> NotFound() => new(GatePassCommandStatus.NotFound);
        public static GatePassCommandResult<T> BadRequest(string errorMessage) => new(GatePassCommandStatus.BadRequest, default, errorMessage);
    }

    public interface IGatePassCommandService
    {
        Task<GatePassResponseDto?> GetByIdAsync(long id, CancellationToken ct = default);
        Task<GatePassCommandResult<GatePassResponseDto>> CreateGatePassAsync(GatePassCreateDto? itemDto, CancellationToken ct = default);
        Task<GatePassCommandResult<GatePassResponseDto>> UpdateGatePassAsync(long id, GatePassUpdateDto? itemDto, CancellationToken ct = default);
        Task<GatePassCommandResult> DeleteGatePassAsync(long id, CancellationToken ct = default);
    }
}
