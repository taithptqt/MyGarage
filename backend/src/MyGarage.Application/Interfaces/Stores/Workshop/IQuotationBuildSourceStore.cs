using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IQuotationBuildSourceStore
{
    Task<QuotationDiagnosisAssignmentInfo?> GetDiagnosisAssignmentInfoAsync(long diagnosisAssignmentId);
    Task<bool> DiagnosisBelongsToServiceOrderAsync(long diagnosisId, long serviceOrderId);
    Task<long?> GetLatestDiagnosisAssignmentIdAsync(long diagnosisId);
    Task<List<Diagnosis>> GetNonCanceledDiagnosesAsync(long serviceOrderId, IReadOnlyList<long>? diagnosisIdFilter);
    Task<List<long>> GetNonCanceledDiagnosisIdsAsync(long serviceOrderId, IReadOnlyList<long>? diagnosisIdFilter);
    Task<List<DiagnosisAssignment>> GetDiagnosisAssignmentsOrderedAsync(IReadOnlyList<long> diagnosisIds);
    Task<List<DiagnosisRecommendation>> GetRecommendationsByAssignmentsOrDiagnosesAsync(
        IReadOnlyList<long> assignmentIds,
        IReadOnlyList<long> diagnosisIds,
        string softDeletedStatus);
    Task<List<DiagnosisRecommendation>> GetRecommendationsByDiagnosesAsync(
        IReadOnlyList<long> diagnosisIds,
        string softDeletedStatus);
    Task<bool> HasRecommendationAsync(
        IReadOnlyList<long> assignmentIds,
        IReadOnlyList<long> diagnosisIds,
        string softDeletedStatus);
    Task<bool> HasRecommendationByDiagnosesAsync(
        IReadOnlyList<long> diagnosisIds,
        string softDeletedStatus);
    Task<Dictionary<long, StandardService>> GetStandardServicesMapAsync(IReadOnlyList<long> serviceIds);
    Task<Dictionary<long, Product>> GetProductsMapAsync(IReadOnlyList<long> productIds);
}
