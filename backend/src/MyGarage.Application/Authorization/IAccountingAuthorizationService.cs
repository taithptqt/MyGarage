using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IAccountingAuthorizationService
{
    Task<bool> CanAccessTableAsync(string tableKey, long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanPostJournalEntryAsync(long journalEntryId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanDecideFinanceApprovalAsync(long approvalId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessFinancialReportsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
}
