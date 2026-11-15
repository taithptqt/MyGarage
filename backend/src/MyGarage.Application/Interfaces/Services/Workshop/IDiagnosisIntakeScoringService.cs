using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisIntakeScoringService
    {
        Task<IReadOnlyList<DiagnosisIntakePackageOptionDto>> GetIntakePackagesAsync();

        Task<DiagnosisIntakeSuggestionDto> BuildIntakeSuggestionAsync(
            long serviceOrderId,
            string? dispatchTag,
            int? bufferMinutesOverride = null,
            string? requestedPackageCode = null);
    }
}
