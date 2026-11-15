using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisRoutingService
    {
        Task<WorkSlot?> ScheduleDiagnosisQueueSlotAsync(long serviceOrderId, int diagnoseMinutes, int bufferMinutes = 15, long? preferredBayId = null);
        Task<bool> IsDispatchTagCompatibleAsync(string? dispatchTag, long? teamId);
        Task<(bool IsCompatible, string Reason)> ValidateTeamBayCompatibilityAsync(long serviceOrderId, long? teamId, long bayId);
        Task<long?> ResolveDiagnoseBayIdForBranchAsync(long branchId, long? preferredBayId = null);
        Task<bool> IsBayInBranchAsync(long bayId, long branchId);
        Task<long?> ResolveDefaultTechnicianForTeamAsync(long teamId);
        Task<DiagnosisDispatchSuggestionDto> BuildDispatchSuggestionAsync(long serviceOrderId, long teamId);
        Task<bool> CanQuickTeamOverflowToGeneralRepairAsync(long serviceOrderId, long branchId);
    }
}
