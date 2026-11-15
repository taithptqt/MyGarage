using MyGarage.Application.Accounting.DTOs;
using MyGarage.Application.Authorization;
using MyGarage.Domain.Rules;
using MyGarage.Models;

namespace MyGarage.Application.Accounting;

public sealed class AccountingApplicationService : IAccountingApplicationService
{
    private readonly IAccountingStore _store;
    private readonly IAccountingAuthorizationService _authService;

    public AccountingApplicationService(
        IAccountingStore store,
        IAccountingAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
    }

    public async Task<DoubleEntryValidationResultDto> ValidateJournalEquilibriumAsync(long journalEntryId, CancellationToken ct = default)
    {
        if (journalEntryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(journalEntryId), "Mã bút toán không hợp lệ.");
        }

        var (totalDebit, totalCredit, lineCount) = await _store.GetJournalLineSumsAsync(journalEntryId, ct);
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

        if (!_store.HasCrudSupport)
        {
            throw new InvalidOperationException("Accounting CRUD service is unavailable.");
        }

        var header = await _store.GetJournalEntryHeaderAsync(journalEntryId, actor.BranchId, ct);
        if (header is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy bút toán #{journalEntryId}.");
        }

        var (status, entryNo) = header.Value;
        var (totalDebit, totalCredit, lineCount) = await _store.GetJournalLineSumsAsync(journalEntryId, ct);

        var (isValid, errorMessage) = AccountingDomainRules.ValidateJournalPosting(status, lineCount, totalDebit, totalCredit);
        if (!isValid)
        {
            throw new InvalidOperationException(errorMessage ?? "Bút toán không đủ điều kiện ghi sổ.");
        }

        var now = VietnamTime.Now;
        var postedBy = actor.UserId ?? 0;
        await _store.MarkJournalEntryPostedAsync(journalEntryId, postedBy, now, ct);

        return new PostJournalResultDto(
            journalEntryId,
            entryNo,
            AccountingDomainRules.JournalStatusPosted,
            now,
            postedBy,
            totalDebit,
            totalCredit
        );
    }

    public Task<bool> IsTableRestrictedAsync(string tableKey)
    {
        return Task.FromResult(AccountingDomainRules.IsTableRestrictedForGenericWrite(tableKey));
    }
}
