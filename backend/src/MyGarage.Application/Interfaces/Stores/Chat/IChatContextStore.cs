using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Chat;

public interface IChatContextStore
{
    Task<Staff?> GetActiveStaffWithRoleAsync(long staffId, CancellationToken ct = default);

    Task<IReadOnlyList<ChatBranchCandidate>> GetActiveChatBranchesAsync(CancellationToken ct = default);

    Task<string?> GetCustomerNoteAsync(long customerId, CancellationToken ct = default);

    Task<IReadOnlyList<RetailOrder>> GetCustomerRetailOrdersForChatAsync(
        long customerId,
        int maxTake,
        CancellationToken ct = default);

    Task<IReadOnlyList<ServiceOrder>> GetCustomerServiceOrdersForChatAsync(
        long customerId,
        int maxTake,
        CancellationToken ct = default);

    Task<IReadOnlyList<ProductImage>> GetProductImagesByProductIdsAsync(
        IReadOnlyCollection<long> productIds,
        CancellationToken ct = default);

    void AddAuditLog(AuditLog auditLog);

    Task SaveChangesWithConcurrencyRetryAsync(CancellationToken ct = default);
}
