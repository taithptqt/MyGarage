using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces;

public interface IQuotationBuildService
{
    Task<QuotationResponseDto> CreateQuotationAsync(long serviceOrderId, QuotationCreateRequestDto request, long? actorStaffId = null);
    Task<QuotationResponseDto> GenerateInitialQuotationFromDiagnosisAsync(long serviceOrderId, long actorStaffId, string? note = null, IReadOnlyList<long>? diagnosisIds = null);
    Task<string?> ValidateInitialQuotationFromDiagnosisAsync(long serviceOrderId, IReadOnlyList<long>? diagnosisIds = null);
    Task<QuotationResponseDto> CloneQuotationAsync(long quotationId, QuotationCloneRequestDto request);
}
