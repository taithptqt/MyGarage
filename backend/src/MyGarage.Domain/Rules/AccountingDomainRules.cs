using System;
using System.Collections.Generic;

namespace MyGarage.Domain.Rules;

public static class AccountingDomainRules
{
    public const decimal BalancingTolerance = 0.005m;

    public const string JournalStatusDraft = "DRAFT";
    public const string JournalStatusPosted = "POSTED";
    public const string JournalStatusReversed = "REVERSED";

    public static readonly HashSet<string> RestrictedGenericWriteTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "supplier-invoices",
        "supplier-invoice-details",
        "supplier-payments",
        "supplier-accounting-profiles",
        "accounting-periods",
        "chart-of-accounts",
        "journal-entries",
        "journal-entry-lines",
        "finance-approval-requests",
        "approval-policies",
        "bank-accounts",
        "bank-transactions",
        "bank-reconciliations",
        "staff-contracts",
        "staff-salary-profiles",
        "payroll-periods",
        "payroll-runs",
        "payroll-items",
        "payslips",
        "salary-advances",
        "employee-loans",
        "attendance-adjustments",
        "overtime-entries",
        "pit-profiles",
        "employee-dependents",
        "tax-filings",
        "legal-documents",
        "document-attachments"
    };

    public static bool IsTableRestrictedForGenericWrite(string? tableKey)
    {
        if (string.IsNullOrWhiteSpace(tableKey)) return false;
        var norm = tableKey.Trim();
        return RestrictedGenericWriteTables.Contains(norm);
    }

    public static (bool IsBalanced, decimal Discrepancy) CheckEquilibrium(decimal totalDebit, decimal totalCredit)
    {
        var discrepancy = Math.Abs(totalDebit - totalCredit);
        return (discrepancy <= BalancingTolerance, discrepancy);
    }

    public static bool CanPostJournal(string? currentStatus, int lineCount, decimal totalDebit, decimal totalCredit)
    {
        if (string.IsNullOrWhiteSpace(currentStatus)) return false;
        if (!string.Equals(currentStatus.Trim(), JournalStatusDraft, StringComparison.OrdinalIgnoreCase)) return false;
        if (lineCount <= 0) return false;
        var (isBalanced, _) = CheckEquilibrium(totalDebit, totalCredit);
        return isBalanced;
    }

    public static (bool CanPost, string? ErrorMessage) ValidateJournalPosting(
        string? currentStatus,
        int lineCount,
        decimal totalDebit,
        decimal totalCredit)
    {
        if (string.IsNullOrWhiteSpace(currentStatus) ||
            !string.Equals(currentStatus.Trim(), JournalStatusDraft, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Only DRAFT journal can be posted.");
        }

        if (lineCount <= 0)
        {
            return (false, "Journal has no lines.");
        }

        var (isBalanced, discrepancy) = CheckEquilibrium(totalDebit, totalCredit);
        if (!isBalanced)
        {
            return (false, $"Posted journal entry must be balanced (discrepancy: {discrepancy:F4}).");
        }

        return (true, null);
    }
}
