using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisService : IGenericService<Diagnosis>
    {
        Task<IReadOnlyList<Diagnosis>> GetAllFilteredAsync(long? serviceOrderId, bool requestOnly);
        Task<ServiceOrder?> GetServiceOrderAsync(long serviceOrderId);
        Task<bool> HasSignedCheckInAsync(long serviceOrderId);
        Task<IReadOnlyList<Diagnosis>> GetAllByServiceOrderAsync(long serviceOrderId);
        Task RemoveRecommendationsByDiagnosisIdsAsync(IEnumerable<long> diagnosisIds);
        Task RemoveDiagnosesAsync(IEnumerable<Diagnosis> diagnoses);
        Task<long> ResolvePlaceholderTechnicianIdAsync(ServiceOrder serviceOrder);
        Task<IReadOnlyList<Diagnosis>> CreatePendingDiagnosesAsync(long serviceOrderId, long technicianId, IReadOnlyList<string> symptoms);
        Task UpdateServiceOrderStatusAsync(ServiceOrder serviceOrder, string status);
        Task<IReadOnlyList<Diagnosis>> GetActiveDiagnosesAsync(long serviceOrderId);
        Task<bool> TechnicianExistsAsync(long technicianId);
        Task AssignDiagnosesAsync(IEnumerable<Diagnosis> diagnoses, long technicianId);
        Task RecallDiagnosesAsync(ServiceOrder serviceOrder, IReadOnlyList<Diagnosis> activeDiagnoses);
        Task<Diagnosis?> GetDiagnosisEntityAsync(long id);
        Task CompleteDiagnosisAsync(Diagnosis diagnosis);
        Task<bool> AreAllDiagnosesCompletedAsync(long serviceOrderId);
    }
}
