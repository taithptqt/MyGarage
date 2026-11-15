using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisDispatchBoardService
    {
        Task<IReadOnlyList<DiagnosisDispatchBoardItemDto>> GetDispatchBoardAsync(
            long? branchId,
            long? teamId,
            bool includeCompleted,
            string? viewMode = null);
    }
}
