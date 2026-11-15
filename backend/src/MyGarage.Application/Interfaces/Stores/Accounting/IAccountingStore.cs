namespace MyGarage.Application.Accounting;

public interface IAccountingStore
{
    bool HasCrudSupport { get; }
    Task<(decimal TotalDebit, decimal TotalCredit, int LineCount)> GetJournalLineSumsAsync(long journalEntryId, CancellationToken ct = default);
    Task<(string Status, string EntryNo)?> GetJournalEntryHeaderAsync(long journalEntryId, long? branchId, CancellationToken ct = default);
    Task MarkJournalEntryPostedAsync(long journalEntryId, long postedByUserId, DateTime now, CancellationToken ct = default);
}
