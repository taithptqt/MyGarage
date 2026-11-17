using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public enum RetailReturnMutationStatus
    {
        Ok,
        Created,
        NotFound,
        BadRequest
    }

    public sealed record RetailReturnCommandResult<T>(
        RetailReturnMutationStatus Status,
        T? Data = default,
        string? ErrorMessage = null);

    public interface IRetailReturnService : IGenericService<RetailReturn>
    {
        List<RetailReturnResponseDto> MapToDtos(IEnumerable<RetailReturn> items);
        RetailReturnResponseDto MapToDto(RetailReturn item);
        Task<RetailReturnResponseDto> CreatePendingDtoAsync(RetailReturnCreateDto itemDto, long actorId);
        Task<(IReadOnlyList<RetailReturnResponseDto> Items, int Total)> GetScopedPagedDtosAsync(
            long? branchId,
            int page,
            int pageSize,
            CancellationToken ct = default);
        Task<RetailReturnResponseDto?> GetScopedDtoByIdAsync(
            long id,
            long? branchId,
            CancellationToken ct = default);
        Task<RetailReturnCommandResult<RetailReturnResponseDto>> CreateScopedPendingAsync(
            RetailReturnCreateDto itemDto,
            long actorId,
            long? branchId,
            CancellationToken ct = default);
        Task<RetailReturnCommandResult<RetailReturnResponseDto>> UpdateScopedPendingAsync(
            long id,
            RetailReturnUpdateDto itemDto,
            long? actorId,
            long? branchId,
            CancellationToken ct = default);
        Task<RetailReturnCommandResult<bool>> DeleteScopedPendingAsync(
            long id,
            long? branchId,
            CancellationToken ct = default);
        Task<RetailReturnFinalizeResult> FinalizeReturnAsync(
            long id,
            long actorUserId,
            long? scopedBranch,
            CancellationToken ct = default);
    }
}
