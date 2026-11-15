using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IDiagnosisAssignmentQueryStore
{
    Task<IReadOnlyList<DiagnosisAssignment>> GetDiagnosisAssignmentsForDiagnosisAsync(long diagnosisId);
    Task<IReadOnlyList<DiagnosisAssignment>> GetDiagnosisAssignmentsByDiagnosisIdsAsync(IReadOnlyCollection<long> diagnosisIds);
    Task<IReadOnlyList<DiagnosisAssignment>> GetDiagnosisAssignmentsByIdsAndDiagnosisIdsAsync(IReadOnlyCollection<long> assignmentIds, IReadOnlyCollection<long> diagnosisIds);
    Task<IReadOnlyList<long>> GetAssignmentIdsByDiagnosisIdsAsync(IReadOnlyCollection<long> diagnosisIds);

    Task<IReadOnlyList<DiagnosisRecommendation>> GetActiveRecommendationsForAssignmentAsync(long diagnosisId, long assignmentId);
    Task<IReadOnlyList<DiagnosisRecommendation>> GetRecommendationsForRemovalAsync(IReadOnlyCollection<long> diagnosisIds, IReadOnlyCollection<long> assignmentIds);
}
