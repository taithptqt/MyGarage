using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisService : IGenericService<Diagnosis>
    {
        Task<IReadOnlyList<Diagnosis>> GetAllFilteredAsync(long? serviceOrderId, bool requestOnly, long? teamId = null);
        Task<ServiceOrder?> GetServiceOrderAsync(long serviceOrderId);
        Task<bool> HasSignedCheckInAsync(long serviceOrderId);
        Task<IReadOnlyList<Diagnosis>> GetAllByServiceOrderAsync(long serviceOrderId);
        Task RemoveRecommendationsByDiagnosisIdsAsync(IEnumerable<long> diagnosisIds);
        Task RemoveDiagnosesAsync(IEnumerable<Diagnosis> diagnoses);
        Task<long> ResolvePlaceholderTechnicianIdAsync(ServiceOrder serviceOrder, string? dispatchTag = null);
        Task<IReadOnlyList<Diagnosis>> CreatePendingDiagnosesAsync(long serviceOrderId, long technicianId, IReadOnlyList<string> symptoms, string? dispatchTag = null);
        Task UpdateServiceOrderStatusAsync(ServiceOrder serviceOrder, string status);
        Task<IReadOnlyList<Diagnosis>> GetActiveDiagnosesAsync(long serviceOrderId);
        Task<bool> TechnicianExistsAsync(long technicianId);
        Task<Team?> GetTeamByIdAsync(long teamId);
        Task<Staff?> GetStaffByIdAsync(long staffId);
        Task<long?> ResolveTeamIdForTechnicianAsync(long technicianId);
        Task<bool> IsTechnicianInTeamAsync(long technicianId, long teamId);
        Task<bool> IsDispatchTagCompatibleAsync(string? dispatchTag, long? teamId);
        Task AssignDiagnosesAsync(IEnumerable<Diagnosis> diagnoses, long technicianId, long? teamId, bool managerOverride);
        Task MarkSupportRequestedAsync(IEnumerable<Diagnosis> diagnoses, long requestedByStaffId, string? note);
        Task RequestCancellationAsync(IEnumerable<Diagnosis> diagnoses, long requestedByStaffId, string reason);
        Task ApplyCancellationDecisionAsync(ServiceOrder serviceOrder, IEnumerable<Diagnosis> diagnoses, bool approve, long managerId, string? note);
        Task<IReadOnlyList<DiagnosisDispatchBoardItemDto>> GetDispatchBoardAsync(long? branchId, long? teamId, bool includeCompleted);
        Task RecallDiagnosesAsync(ServiceOrder serviceOrder, IReadOnlyList<Diagnosis> activeDiagnoses);
        Task<Diagnosis?> GetDiagnosisEntityAsync(long id);
        Task CompleteDiagnosisAsync(Diagnosis diagnosis);
        Task<bool> AreAllDiagnosesCompletedAsync(long serviceOrderId);
    }
}
