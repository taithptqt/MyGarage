using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Chatbot;

public interface IChatbotLoyaltyVoucherStore
{
    Task<ChatbotCustomerTierSnapshot?> GetCustomerTierSnapshotAsync(long customerId, CancellationToken ct = default);
    Task<ChatbotNextTierSnapshot?> GetNextCustomerTierAsync(decimal totalSpending, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotVoucherLookupRow>> GetDirectCustomerVouchersAsync(long customerId, DateTime nowUtc, CancellationToken ct = default);
    Task<IReadOnlyList<long>> GetExcludedCustomerVoucherIdsAsync(long customerId, DateTime nowUtc, CancellationToken ct = default);
    Task<IReadOnlyList<ChatbotVoucherLookupRow>> GetAssignedCustomerVouchersAsync(long customerId, DateTime nowUtc, IReadOnlyCollection<long> excludedVoucherIds, CancellationToken ct = default);
    Task<Dictionary<long, int>> GetCustomerVoucherRedemptionCountsAsync(long customerId, CancellationToken ct = default);
    Task<long?> GetCustomerTierIdAsync(long customerId, CancellationToken ct = default);
    Task<ChatbotVehicleContextSnapshot?> GetVehicleContextAsync(long vehicleId, CancellationToken ct = default);
    Task<List<Voucher>> GetCandidateVouchersForResolverAsync(long customerId, DateTime nowLocal, CancellationToken ct = default);
}
