using MyGarage.Application.Accounting;
using MyGarage.Application.Common;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Accounting;

public sealed class EfAccountingStore : IAccountingStore
{
    private readonly IAccountingTableQueryService? _tableQuery;
    private readonly IAccountingSqlQueryService? _sqlQuery;
    private readonly IAccountingSqlCommandService? _sqlCommand;

    public EfAccountingStore(
        IAccountingTableQueryService? tableQuery = null,
        IAccountingSqlQueryService? sqlQuery = null,
        IAccountingSqlCommandService? sqlCommand = null)
    {
        _tableQuery = tableQuery;
        _sqlQuery = sqlQuery;
        _sqlCommand = sqlCommand;
    }

    public bool HasCrudSupport => _tableQuery != null && _sqlQuery != null && _sqlCommand != null;

    private static string AsString(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null ? Convert.ToString(v) ?? "" : "";

    private static decimal AsDecimal(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToDecimal(v) : 0m;

    private static int AsInt(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToInt32(v) : 0;

    public async Task<(decimal TotalDebit, decimal TotalCredit, int LineCount)> GetJournalLineSumsAsync(
        long journalEntryId,
        CancellationToken ct = default)
    {
        if (_sqlQuery == null)
        {
            return (0m, 0m, 0);
        }

        var sums = (await _sqlQuery.QueryAsync(
            "SELECT ISNULL(SUM(debit_amount),0) AS total_debit, ISNULL(SUM(credit_amount),0) AS total_credit, COUNT(*) AS line_count FROM dbo.journal_entry_lines WHERE journal_entry_id=@id;",
            [new QueryParam("@id", journalEntryId)])).FirstOrDefault();

        var totalDebit = AsDecimal(sums, "total_debit");
        if (totalDebit == 0m) totalDebit = AsDecimal(sums, "totalDebit");

        var totalCredit = AsDecimal(sums, "total_credit");
        if (totalCredit == 0m) totalCredit = AsDecimal(sums, "totalCredit");

        var lineCount = AsInt(sums, "line_count");
        if (lineCount == 0) lineCount = AsInt(sums, "lineCount");

        return (totalDebit, totalCredit, lineCount);
    }

    public async Task<(string Status, string EntryNo)?> GetJournalEntryHeaderAsync(
        long journalEntryId,
        long? branchId,
        CancellationToken ct = default)
    {
        if (_tableQuery == null)
        {
            return null;
        }

        var row = await _tableQuery.GetByIdAsync("journal-entries", journalEntryId, branchId);
        if (row is null)
        {
            return null;
        }

        var status = AsString(row, "status");
        var entryNo = AsString(row, "entry_no");
        if (string.IsNullOrWhiteSpace(entryNo)) entryNo = AsString(row, "entryNo");

        return (status, entryNo);
    }

    public async Task MarkJournalEntryPostedAsync(
        long journalEntryId,
        long postedByUserId,
        DateTime now,
        CancellationToken ct = default)
    {
        if (_sqlCommand == null)
        {
            throw new InvalidOperationException("Accounting CRUD service is unavailable.");
        }

        await _sqlCommand.ExecuteAsync(
            "UPDATE dbo.journal_entries SET status=@status, posted_by=@u, posted_at=@postedAt, updated_at=@updatedAt WHERE id=@id;",
            [
                new QueryParam("@status", AccountingDomainRules.JournalStatusPosted),
                new QueryParam("@u", postedByUserId),
                new QueryParam("@postedAt", now),
                new QueryParam("@updatedAt", now),
                new QueryParam("@id", journalEntryId)
            ]);
    }
}
