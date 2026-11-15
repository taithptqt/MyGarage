using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisRecallCompletionService
    {
        Task<int> CloneRejectedDiagnosesVersionAsync(IEnumerable<Diagnosis> diagnoses, long actorId, string? note = null);
        Task RecallDiagnosesAsync(ServiceOrder serviceOrder, IReadOnlyList<Diagnosis> activeDiagnoses);
        Task<IReadOnlyList<Diagnosis>> RequeueRecalledDiagnosesAsync(
            IEnumerable<Diagnosis> recalledDiagnoses,
            long? assignedTeamId,
            long? assignedBayId,
            string? dispatchTag = null,
            string? assignmentNote = null,
            long? actorStaffId = null);
        Task CompleteDiagnosisAsync(Diagnosis diagnosis);
        Task<bool> AreAllDiagnosesCompletedAsync(long serviceOrderId);
    }
}
