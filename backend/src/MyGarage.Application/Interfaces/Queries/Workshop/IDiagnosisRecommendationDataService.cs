using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record SupplementaryNotificationTarget(
    long ServiceOrderId,
    string? Code,
    long? ExecuteBranchId,
    long? OriginBranchId,
    long? ReceptionistId,
    long? ServiceAdvisorId);

public interface IDiagnosisRecommendationDataService
{
    Task<(IReadOnlyList<DiagnosisRecommendation> Items, int Total)> GetPagedAsync(
        long? diagnosisId,
        long? diagnosisAssignmentId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<PendingSupplementaryProposalResponseDto>> GetPendingSupplementaryAsync(
        long? serviceOrderId,
        long? branchId,
        CancellationToken ct = default);

    Task<long> ResolveServiceOrderIdFromDiagnosisIdAsync(long diagnosisId, CancellationToken ct = default);

    Task<long> ResolveServiceOrderIdFromAssignmentIdAsync(long diagnosisAssignmentId, CancellationToken ct = default);

    Task<(long DiagnosisId, long DiagnosisAssignmentId)?> ResolveDiagnosisLinkAsync(
        long? diagnosisAssignmentId,
        long? diagnosisId,
        CancellationToken ct = default);

    Task<SupplementaryNotificationTarget?> GetSupplementaryNotificationTargetAsync(
        long diagnosisId,
        CancellationToken ct = default);

    Task SyncChildMaterialQuantitiesByBomAsync(
        DiagnosisRecommendation parentRecommendation,
        CancellationToken ct = default);
}
