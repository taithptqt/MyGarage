using System;
using System.Collections.Generic;

namespace MyGarage.Application.Accounting.DTOs;

public sealed record JournalEntryLineDto(
    long Id,
    long JournalEntryId,
    long AccountId,
    long? BranchId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description,
    long? PaymentId,
    long? InvoiceId
);

public sealed record JournalEntryDto(
    long Id,
    string EntryNo,
    DateTime EntryDate,
    string Status,
    string? Description,
    long? BranchId,
    long? PeriodId,
    DateTime? PostedAt,
    long? PostedBy,
    IReadOnlyList<JournalEntryLineDto> Lines
);

public sealed record PostJournalInput(
    string? Note
);

public sealed record PostJournalResultDto(
    long JournalEntryId,
    string EntryNo,
    string Status,
    DateTime PostedAt,
    long PostedBy,
    decimal TotalDebit,
    decimal TotalCredit
);

public sealed record DoubleEntryValidationResultDto(
    long JournalEntryId,
    bool IsBalanced,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Discrepancy,
    int LineCount,
    string? ValidationMessage
);
