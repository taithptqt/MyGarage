using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Accounting.DTOs;
using MyGarage.Application.Authorization;

namespace MyGarage.Application.Accounting;

public interface IAccountingApplicationService
{
    Task<DoubleEntryValidationResultDto> ValidateJournalEquilibriumAsync(long journalEntryId, CancellationToken ct = default);
    Task<PostJournalResultDto> PostJournalEntryAsync(long journalEntryId, PostJournalInput input, UserActorContext actor, CancellationToken ct = default);
    Task<bool> IsTableRestrictedAsync(string tableKey);
}
