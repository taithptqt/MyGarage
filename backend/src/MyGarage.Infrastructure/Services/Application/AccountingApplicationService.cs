using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Accounting;
using MyGarage.Application.Accounting.DTOs;
using MyGarage.Application.Authorization;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Support;

namespace MyGarage.Services;

public sealed class AccountingApplicationService : IAccountingApplicationService
{
    private readonly IGarageContext _context;
    private readonly IAccountingAuthorizationService _authService;
    private readonly IAccountingCrudService? _crud;

    public AccountingApplicationService(
        IGarageContext context,
        IAccountingAuthorizationService authService,
        IAccountingCrudService? crud = null)
    {
        _context = context;
        _authService = authService;
        _crud = crud;
    }

    private static string AsString(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null ? Convert.ToString(v) ?? "" : "";

    private static decimal AsDecimal(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToDecimal(v) : 0m;

    private static int AsInt(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToInt32(v) : 0;

    public async Task<DoubleEntryValidationResultDto> ValidateJournalEquilibriumAsync(long journalEntryId, CancellationToken ct = default)
    {
        if (journalEntryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(journalEntryId), "Mã bút toán không hợp lệ.");
        }

        decimal totalDebit = 0m;
        decimal totalCredit = 0m;
        int lineCount = 0;

        if (_crud != null)
        {
            var sums = (await _crud.QueryAsync(
                "SELECT ISNULL(SUM(debit_amount),0) AS total_debit, ISNULL(SUM(credit_amount),0) AS total_credit, COUNT(*) AS line_count FROM dbo.journal_entry_lines WHERE journal_entry_id=@id;",
                [new SqlParameter("@id", journalEntryId)])).FirstOrDefault();

            totalDebit = AsDecimal(sums, "total_debit");
            if (totalDebit == 0m) totalDebit = AsDecimal(sums, "totalDebit");

            totalCredit = AsDecimal(sums, "total_credit");
            if (totalCredit == 0m) totalCredit = AsDecimal(sums, "totalCredit");

            lineCount = AsInt(sums, "line_count");
            if (lineCount == 0) lineCount = AsInt(sums, "lineCount");
        }

        var (isBalanced, discrepancy) = AccountingDomainRules.CheckEquilibrium(totalDebit, totalCredit);

        string? validationMessage = isBalanced
            ? "Bút toán cân đối (tổng nợ = tổng có)."
            : $"Bút toán mất cân đối: Chênh lệch {discrepancy:N2} (Tổng Nợ: {totalDebit:N2}, Tổng Có: {totalCredit:N2}).";

        return new DoubleEntryValidationResultDto(
            journalEntryId,
            isBalanced,
            totalDebit,
            totalCredit,
            discrepancy,
            lineCount,
            validationMessage
        );
    }

    public async Task<PostJournalResultDto> PostJournalEntryAsync(
        long journalEntryId,
        PostJournalInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (journalEntryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(journalEntryId), "Mã bút toán không hợp lệ.");
        }

        var canPost = await _authService.CanPostJournalEntryAsync(journalEntryId, actor, ct);
        if (!canPost)
        {
            throw new UnauthorizedAccessException("Không có quyền ghi sổ (POST) bút toán kế toán.");
        }

        if (_crud == null)
        {
            throw new InvalidOperationException("Accounting CRUD service is unavailable.");
        }

        var row = await _crud.GetByIdAsync("journal-entries", journalEntryId, actor.BranchId);
        if (row is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy bút toán #{journalEntryId}.");
        }

        var status = AsString(row, "status");
        var entryNo = AsString(row, "entry_no");
        if (string.IsNullOrWhiteSpace(entryNo)) entryNo = AsString(row, "entryNo");

        var sums = (await _crud.QueryAsync(
            "SELECT ISNULL(SUM(debit_amount),0) AS total_debit, ISNULL(SUM(credit_amount),0) AS total_credit, COUNT(*) AS line_count FROM dbo.journal_entry_lines WHERE journal_entry_id=@id;",
            [new SqlParameter("@id", journalEntryId)])).FirstOrDefault();

        var totalDebit = AsDecimal(sums, "total_debit");
        if (totalDebit == 0m) totalDebit = AsDecimal(sums, "totalDebit");

        var totalCredit = AsDecimal(sums, "total_credit");
        if (totalCredit == 0m) totalCredit = AsDecimal(sums, "totalCredit");

        var lineCount = AsInt(sums, "line_count");
        if (lineCount == 0) lineCount = AsInt(sums, "lineCount");

        var (isValid, errorMessage) = AccountingDomainRules.ValidateJournalPosting(status, lineCount, totalDebit, totalCredit);
        if (!isValid)
        {
            throw new InvalidOperationException(errorMessage ?? "Bút toán không đủ điều kiện ghi sổ.");
        }

        var now = VietnamTime.Now;
        await _crud.ExecuteAsync(
            "UPDATE dbo.journal_entries SET status=@status, posted_by=@u, posted_at=@postedAt, updated_at=@updatedAt WHERE id=@id;",
            [
                new SqlParameter("@status", AccountingDomainRules.JournalStatusPosted),
                new SqlParameter("@u", actor.UserId ?? 0),
                new SqlParameter("@postedAt", now),
                new SqlParameter("@updatedAt", now),
                new SqlParameter("@id", journalEntryId)
            ]);

        return new PostJournalResultDto(
            journalEntryId,
            entryNo,
            AccountingDomainRules.JournalStatusPosted,
            now,
            actor.UserId ?? 0,
            totalDebit,
            totalCredit
        );
    }

    public Task<bool> IsTableRestrictedAsync(string tableKey)
    {
        return Task.FromResult(AccountingDomainRules.IsTableRestrictedForGenericWrite(tableKey));
    }
}
