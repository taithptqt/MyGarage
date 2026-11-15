using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IDiagnosisRecordQueryStore
{
    Task<Diagnosis?> GetDiagnosisByIdAsync(long id);
    Task<IReadOnlyList<Diagnosis>> GetFilteredDiagnosesNoTrackingAsync(long? serviceOrderId, bool requestOnly);
    Task<IReadOnlyList<Diagnosis>> GetDiagnosesByServiceOrderAsync(long serviceOrderId);
    Task<IReadOnlyList<Diagnosis>> GetActiveDiagnosesByServiceOrderAsync(long serviceOrderId);
    Task<IReadOnlyList<Diagnosis>> GetDispatchBoardDiagnosesNoTrackingAsync(bool includeCompleted);
    Task<IReadOnlyList<string?>> GetDiagnosisStatusesByServiceOrderAsync(long serviceOrderId);
}
