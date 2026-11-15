namespace MyGarage.Application.Interfaces.Notifications;

public sealed record SupplementaryProposalRealtimeEvent(
    string Action,
    long ServiceOrderId,
    string? ServiceOrderCode,
    long DiagnosisId,
    long RecommendationId,
    long? ExecuteBranchId,
    long? OriginBranchId,
    DateTime OccurredAt);

public interface IDiagnosisRealtimeNotifier
{
    Task NotifySupplementaryProposalUpdatedAsync(
        IReadOnlyCollection<long> recipientStaffIds,
        SupplementaryProposalRealtimeEvent evt,
        CancellationToken ct = default);
}
