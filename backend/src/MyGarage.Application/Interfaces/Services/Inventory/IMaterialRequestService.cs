using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IMaterialRequestService : IGenericService<MaterialRequest>
    {
        Task<MaterialRequest?> CreateFromQuotationAsync(long quotationId, long actorId);
        Task<MaterialRequest> IssuePartsAsync(MaterialRequestIssuanceDto request);
        Task<MaterialRequest> RejectRequestAsync(long requestId, long actorStaffId, string? reason);
        Task<MaterialRequestResponseDto> CreateDtoAsync(MaterialRequestCreateDto itemDto, long? defaultTechnicianId = null);
        Task<MaterialRequestResponseDto?> UpdateDtoAsync(long id, MaterialRequestUpdateDto itemDto, MaterialRequest? existingItem = null);
        Task<PagedEnvelope<MaterialRequestSummaryDto>> GetSummaryPageAsync(
            long? scopedBranchId,
            string? status,
            int? page,
            int? pageSize,
            CancellationToken ct = default);
        Task<MaterialRequestCommandResult<MaterialRequestSummaryDto>> GetScopedSummaryByIdAsync(
            long id,
            long? scopedBranchId,
            CancellationToken ct = default);
        Task<MaterialRequestCommandResult<MaterialRequestResponseDto>> CreateScopedAsync(
            MaterialRequestCreateDto? itemDto,
            long? scopedBranchId,
            long? actorUserId,
            CancellationToken ct = default);
        Task<MaterialRequestCommandResult<MaterialRequestResponseDto>> UpdateScopedAsync(
            long id,
            MaterialRequestUpdateDto? itemDto,
            long? scopedBranchId,
            CancellationToken ct = default);
        Task<MaterialRequestCommandResult<MaterialRequestSummaryDto>> IssueScopedAsync(
            long id,
            long? scopedBranchId,
            long? currentUserId,
            long? fallbackActorStaffId,
            string? note,
            IReadOnlyList<MaterialRequestIssuanceItemDto>? requestedItems,
            CancellationToken ct = default);
        Task<MaterialRequestCommandResult<MaterialRequestSummaryDto>> RejectScopedAsync(
            long id,
            long? scopedBranchId,
            long? currentUserId,
            long? fallbackActorStaffId,
            string? reason,
            CancellationToken ct = default);
        Task<MaterialRequestCommandResult<bool>> DeleteScopedAsync(
            long id,
            long? scopedBranchId,
            CancellationToken ct = default);
    }
}
