using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public enum QcMutationStatus
    {
        Ok,
        Created,
        NotFound,
        BadRequest
    }

    public sealed record QcMutationResult<T>(
        QcMutationStatus Status,
        T? Data = default,
        string? ErrorMessage = null);

    public interface IQcInspectionService : IGenericService<QcInspection>
    {
        List<QcInspectionResponseDto> MapToDtos(IEnumerable<QcInspection> items);
        Task<QcInspectionResponseDto?> GetDtoByIdAsync(long id);
        Task<QcInspectionResponseDto> CreateDtoAsync(QcInspectionCreateDto itemDto);
        Task<QcInspectionResponseDto?> UpdateDtoAsync(long id, QcInspectionUpdateDto itemDto);
        Task<bool> PassAsync(long id, QcPassRequestDto request);
        Task<bool> FailAsync(long id, QcFailRequestDto request);
        Task<(IReadOnlyList<QcInspectionResponseDto> Items, int Total)> GetPagedDtosAsync(
            long? serviceOrderId,
            int page,
            int pageSize,
            CancellationToken ct = default);
        Task<QcMutationResult<QcInspectionResponseDto>> CreateWithSeparationOfDutiesAsync(
            QcInspectionCreateDto itemDto,
            long? actorUserId,
            CancellationToken ct = default);
        Task<QcMutationResult<object>> UpsertChecklistItemsAsync(
            long id,
            QcInspectionItemsUpsertRequestDto? request,
            long? actorUserId,
            CancellationToken ct = default);
        Task<QcMutationResult<bool>> PassAsync(
            long id,
            QcPassRequestDto request,
            long? actorUserId,
            CancellationToken ct = default);
        Task<QcMutationResult<bool>> FailAsync(
            long id,
            QcFailRequestDto request,
            long? actorUserId,
            CancellationToken ct = default);
    }
}
