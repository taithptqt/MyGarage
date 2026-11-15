using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public enum DiagnosisRecommendationCommandStatus
    {
        Ok = 0,
        NoContent = 1,
        NotFound = 2,
        BadRequest = 3
    }

    public sealed record DiagnosisRecommendationCommandResult(
        DiagnosisRecommendationCommandStatus Status,
        string? ErrorMessage = null)
    {
        public static DiagnosisRecommendationCommandResult NoContent() => new(DiagnosisRecommendationCommandStatus.NoContent);
        public static DiagnosisRecommendationCommandResult NotFound() => new(DiagnosisRecommendationCommandStatus.NotFound);
        public static DiagnosisRecommendationCommandResult BadRequest(string errorMessage) => new(DiagnosisRecommendationCommandStatus.BadRequest, errorMessage);
    }

    public sealed record DiagnosisRecommendationCommandResult<T>(
        DiagnosisRecommendationCommandStatus Status,
        T? Data = default,
        string? ErrorMessage = null)
    {
        public static DiagnosisRecommendationCommandResult<T> Ok(T data) => new(DiagnosisRecommendationCommandStatus.Ok, data);
        public static DiagnosisRecommendationCommandResult<T> NotFound() => new(DiagnosisRecommendationCommandStatus.NotFound);
        public static DiagnosisRecommendationCommandResult<T> BadRequest(string errorMessage) => new(DiagnosisRecommendationCommandStatus.BadRequest, default, errorMessage);
    }

    public interface IDiagnosisRecommendationCommandService
    {
        Task<PagedEnvelope<DiagnosisRecommendationResponseDto>> GetAllAsync(
            long? diagnosisId,
            long? diagnosisAssignmentId,
            int? page = null,
            int? pageSize = null,
            CancellationToken ct = default);

        Task<IReadOnlyList<PendingSupplementaryProposalResponseDto>> GetPendingSupplementaryAsync(
            long? serviceOrderId = null,
            long? branchId = null,
            CancellationToken ct = default);

        Task<DiagnosisRecommendationResponseDto?> GetByIdAsync(long id, CancellationToken ct = default);

        Task<DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>> CreateAsync(
            DiagnosisRecommendationCreateDto? itemDto,
            CancellationToken ct = default);

        Task<DiagnosisRecommendationCommandResult<DiagnosisRecommendationResponseDto>> UpdateAsync(
            long id,
            DiagnosisRecommendationUpdateDto? itemDto,
            CancellationToken ct = default);

        Task<DiagnosisRecommendationCommandResult> DeleteAsync(long id, CancellationToken ct = default);
    }
}
