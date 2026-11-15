using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public sealed record QuotationCustomerDecisionRealtimeEvent(
    bool Approved,
    long QuotationId,
    string QuotationCode,
    long ServiceOrderId,
    string ServiceOrderCode,
    string VehiclePlate,
    DateTime? DecidedAt,
    long ExecuteBranchId,
    long? ServiceAdvisorId,
    IReadOnlyList<long> ForemanIds,
    IReadOnlyList<long> StorekeeperIds);

public sealed record SupplementaryReductionRealtimeEvent(
    long ServiceOrderId,
    string ServiceOrderCode,
    long QuotationId,
    long QuotationDetailId,
    string? ItemName,
    string? CancelReason,
    DateTime OccurredAt);

public interface IQuotationRealtimeNotifier
{
    Task NotifyCustomerDecisionAsync(QuotationCustomerDecisionRealtimeEvent evt);
    Task NotifySupplementaryReductionSavedAsync(IReadOnlyList<long> recipientIds, SupplementaryReductionRealtimeEvent evt);
}
