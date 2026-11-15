using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisCrudService : IGenericService<Diagnosis>
    {
        Task<Diagnosis?> GetDiagnosisEntityAsync(long id);
        Task<IReadOnlyList<Diagnosis>> CreatePendingDiagnosesAsync(
            long serviceOrderId,
            long? technicianId,
            IReadOnlyList<string> symptoms,
            string? dispatchTag = null,
            long? assignedTeamId = null,
            long? assignedBayId = null,
            string? assignmentNote = null);
        Task RemoveRecommendationsByDiagnosisIdsAsync(IEnumerable<long> diagnosisIds);
        Task RemoveDiagnosesAsync(IEnumerable<Diagnosis> diagnoses);
        Task ArchiveDiagnosesAsSupersededAsync(IEnumerable<Diagnosis> diagnoses, long? actorStaffId = null, string? auditTag = null);
        Task UpdateServiceOrderStatusAsync(ServiceOrder serviceOrder, string status);
    }
}
