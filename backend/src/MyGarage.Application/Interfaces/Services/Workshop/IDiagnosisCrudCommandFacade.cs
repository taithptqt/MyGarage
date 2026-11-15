using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisCrudCommandFacade
    {
        Task<IReadOnlyList<DiagnosisResponseDto>> GetAllAsync(long? serviceOrderId, bool requestOnly, long? teamId);
        Task<DiagnosisResponseDto?> GetByIdAsync(long id);
        Task<DiagnosisResponseDto> CreateAsync(DiagnosisCreateDto itemDto);
        Task<DiagnosisResponseDto?> UpdateAsync(long id, DiagnosisUpdateDto itemDto);
        Task<DiagnosisCommandResult<DiagnosisResponseDto>> CompleteAsync(long id, string? symptom, string? rootCause, string? severity);
    }
}
