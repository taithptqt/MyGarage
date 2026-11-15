using System.Globalization;
using System.Text;
using System.Text.Json;
using MyGarage.Application.Exceptions;
using MyGarage.Interfaces;
using MyGarage.Services;

namespace MyGarage.Application.Accounting;

public sealed class AccountingTableAndInvoiceService : IAccountingTableAndInvoiceService
{
    private static readonly HashSet<string> RestrictedGenericWriteTables = new(StringComparer.OrdinalIgnoreCase)
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

    private readonly IAccountingTableQueryService _tableQuery;
    private readonly IAccountingTableCommandService _tableCommand;
    private readonly IAccountingValidationService _validationService;

    public AccountingTableAndInvoiceService(
        IAccountingTableQueryService tableQuery,
        IAccountingTableCommandService tableCommand,
        IAccountingValidationService validationService)
    {
        _tableQuery = tableQuery;
        _tableCommand = tableCommand;
        _validationService = validationService;
    }

    public AccountingAccessResult AuthorizeTableAccess(string tableKey, AccountingActorContext actor)
    {
        return IsFinanceApprovalTableKey(tableKey)
            ? AuthorizeFinanceApprovalAccess(actor)
            : AuthorizeAccountingAccess(actor);
    }

    public AccountingAccessResult AuthorizeFinanceApprovalAccess(AccountingActorContext actor)
    {
        if (!actor.IsAuthenticated)
            return new AccountingAccessResult(false, DeniedStatusCode: 401, DeniedMessage: "Unauthorized.");

        if (!IsAccountingStaff(actor.ClaimRole, actor.RoleCode, actor.RoleName) &&
            !IsManagerStaff(actor.ClaimRole, actor.RoleCode, actor.RoleName) &&
            !IsAdminStaff(actor.ClaimRole, actor.RoleCode, actor.RoleName))
        {
            return new AccountingAccessResult(false, DeniedStatusCode: 403);
        }

        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
            return new AccountingAccessResult(false, DeniedStatusCode: 401, DeniedMessage: "Invalid user identity.");

        return new AccountingAccessResult(true, new AccountingAccess(actor.UserId.Value, actor.BranchId));
    }

    public AccountingAccessResult AuthorizeAccountingAccess(AccountingActorContext actor)
    {
        if (!actor.IsAuthenticated)
            return new AccountingAccessResult(false, DeniedStatusCode: 401, DeniedMessage: "Unauthorized.");

        var isAccountingStaff = IsAccountingStaff(actor.ClaimRole, actor.RoleCode, actor.RoleName);
        var isManagerStaff = IsManagerStaff(actor.ClaimRole, actor.RoleCode, actor.RoleName);
        var isHrManagerStaff = IsHrManagerStaff(actor.ClaimRole, actor.RoleCode, actor.RoleName);
        var isAdminStaff = IsAdminStaff(actor.ClaimRole, actor.RoleCode, actor.RoleName);
        if (!isAccountingStaff && !isManagerStaff && !isHrManagerStaff && !isAdminStaff)
            return new AccountingAccessResult(false, DeniedStatusCode: 403);

        if (!actor.UserId.HasValue || actor.UserId.Value <= 0)
            return new AccountingAccessResult(false, DeniedStatusCode: 401, DeniedMessage: "Invalid user identity.");

        if (!actor.BranchId.HasValue && !isAdminStaff)
            return new AccountingAccessResult(false, DeniedStatusCode: 403);

        return new AccountingAccessResult(true, new AccountingAccess(actor.UserId.Value, actor.BranchId));
    }

    public async Task<AccountingCommandResult> GetTableAsync(string tableKey, int limit, long? branchId)
    {
        try
        {
            return new AccountingCommandResult(200, await _tableQuery.GetAllAsync(tableKey, limit, branchId));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return AccountingCommandResult.Problem(404, ex.Message);
        }
    }

    public async Task<AccountingCommandResult> GetTableRowByIdAsync(string tableKey, long id, long? branchId)
    {
        try
        {
            var row = await _tableQuery.GetByIdAsync(tableKey, id, branchId);
            return row is null ? new AccountingCommandResult(404) : new AccountingCommandResult(200, row);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return AccountingCommandResult.Problem(404, ex.Message);
        }
    }

    public async Task<AccountingCommandResult> CreateTableRowAsync(string tableKey, JsonElement payload, long? branchId)
    {
        if (RestrictedGenericWriteTables.Contains(tableKey))
        {
            return AccountingCommandResult.Problem(405, $"Generic write is disabled for accounting-critical table '{tableKey}'. Use a dedicated workflow endpoint.");
        }

        try
        {
            var validationError = await ValidateGenericTableWriteAsync(tableKey, payload, branchId, null);
            if (!string.IsNullOrWhiteSpace(validationError))
                return AccountingCommandResult.Problem(400, validationError);

            var created = await _tableCommand.InsertAsync(tableKey, payload, branchId);
            return created is null
                ? AccountingCommandResult.Problem(400, "Failed to create row.")
                : new AccountingCommandResult(200, created);
        }
        catch (Exception ex) when (ex is DuplicateKeyException or DatabaseOperationException)
        {
            return AccountingCommandResult.Problem(400, ex.Message);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return AccountingCommandResult.Problem(400, ex.Message);
        }
    }

    public async Task<AccountingCommandResult> UpdateTableRowAsync(string tableKey, long id, JsonElement payload, long? branchId)
    {
        if (RestrictedGenericWriteTables.Contains(tableKey))
        {
            return AccountingCommandResult.Problem(405, $"Generic write is disabled for accounting-critical table '{tableKey}'. Use a dedicated workflow endpoint.");
        }

        try
        {
            var validationError = await ValidateGenericTableWriteAsync(tableKey, payload, branchId, id);
            if (!string.IsNullOrWhiteSpace(validationError))
                return AccountingCommandResult.Problem(400, validationError);

            var updated = await _tableCommand.UpdateAsync(tableKey, id, payload, branchId);
            return updated is null ? new AccountingCommandResult(404) : new AccountingCommandResult(200, updated);
        }
        catch (Exception ex) when (ex is DuplicateKeyException or DatabaseOperationException)
        {
            return AccountingCommandResult.Problem(400, ex.Message);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return AccountingCommandResult.Problem(400, ex.Message);
        }
    }

    public async Task<AccountingCommandResult> DeleteTableRowAsync(string tableKey, long id, long? branchId)
    {
        if (RestrictedGenericWriteTables.Contains(tableKey))
        {
            return AccountingCommandResult.Problem(405, $"Generic write is disabled for accounting-critical table '{tableKey}'. Use a dedicated workflow endpoint.");
        }

        try
        {
            return await _tableCommand.DeleteAsync(tableKey, id, branchId)
                ? new AccountingCommandResult(204)
                : new AccountingCommandResult(404);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return AccountingCommandResult.Problem(404, ex.Message);
        }
    }

    public async Task<AccountingCommandResult> CreateSupplierInvoiceAsync(JsonElement payload, long? branchId)
    {
        var error = await _validationService.ValidateSupplierInvoice3WayMatchingAsync(payload, branchId);
        if (!string.IsNullOrWhiteSpace(error))
            return AccountingCommandResult.Problem(400, error);

        var created = await _tableCommand.InsertAsync("supplier-invoices", payload, branchId);
        return created is null
            ? AccountingCommandResult.Problem(400, "Failed to create supplier invoice.")
            : new AccountingCommandResult(200, created);
    }

    public async Task<AccountingCommandResult> UpdateSupplierInvoiceAsync(long id, JsonElement payload, long? branchId)
    {
        var error = await _validationService.ValidateSupplierInvoice3WayMatchingAsync(payload, branchId, id);
        if (!string.IsNullOrWhiteSpace(error))
            return AccountingCommandResult.Problem(400, error);

        var updated = await _tableCommand.UpdateAsync("supplier-invoices", id, payload, branchId);
        return updated is null ? new AccountingCommandResult(404) : new AccountingCommandResult(200, updated);
    }

    public async Task<AccountingCommandResult> CreateSupplierInvoiceDetailAsync(JsonElement payload, long? branchId)
    {
        var error = await _validationService.ValidateSupplierInvoiceDetail3WayMatchingAsync(payload, branchId);
        if (!string.IsNullOrWhiteSpace(error))
            return AccountingCommandResult.Problem(400, error);

        var created = await _tableCommand.InsertAsync("supplier-invoice-details", payload, branchId);
        return created is null
            ? AccountingCommandResult.Problem(400, "Failed to create supplier invoice detail.")
            : new AccountingCommandResult(200, created);
    }

    public async Task<AccountingCommandResult> UpdateSupplierInvoiceDetailAsync(long id, JsonElement payload, long? branchId)
    {
        var error = await _validationService.ValidateSupplierInvoiceDetail3WayMatchingAsync(payload, branchId, id);
        if (!string.IsNullOrWhiteSpace(error))
            return AccountingCommandResult.Problem(400, error);

        var updated = await _tableCommand.UpdateAsync("supplier-invoice-details", id, payload, branchId);
        return updated is null ? new AccountingCommandResult(404) : new AccountingCommandResult(200, updated);
    }

    private async Task<string?> ValidateGenericTableWriteAsync(string tableKey, JsonElement payload, long? branchId, long? id)
    {
        if (tableKey.Equals("supplier-invoices", StringComparison.OrdinalIgnoreCase))
            return await _validationService.ValidateSupplierInvoice3WayMatchingAsync(payload, branchId, id);

        if (tableKey.Equals("supplier-payments", StringComparison.OrdinalIgnoreCase))
            return await _validationService.ValidateSupplierPaymentAsync(payload, branchId, id);

        return null;
    }

    private static bool IsFinanceApprovalTableKey(string tableKey) =>
        tableKey.Equals("finance-approval-requests", StringComparison.OrdinalIgnoreCase);

    private static bool IsAccountingStaff(string? claimRole, string? roleCode, string? roleName)
    {
        var normalizedRoleName = NormalizeRoleNameForMatch(roleName);
        var normalizedClaimRole = (claimRole ?? string.Empty).Trim();
        return string.Equals(roleCode, "ACCOUNTANT", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedClaimRole, "ACCOUNTANT", StringComparison.OrdinalIgnoreCase) ||
               (string.Equals(normalizedClaimRole, "Staff", StringComparison.OrdinalIgnoreCase) &&
                (normalizedRoleName.Contains("ACCOUNTANT", StringComparison.Ordinal) ||
                 normalizedRoleName.Contains("KE TOAN", StringComparison.Ordinal))) ||
               normalizedRoleName.Contains("ACCOUNTANT", StringComparison.Ordinal) ||
               normalizedRoleName.Contains("KE TOAN", StringComparison.Ordinal);
    }

    private static bool IsHrManagerStaff(string? claimRole, string? roleCode, string? roleName)
    {
        var normalizedRoleName = NormalizeRoleNameForMatch(roleName);
        var normalizedClaimRole = (claimRole ?? string.Empty).Trim();
        return string.Equals(roleCode, "HR_MANAGER", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedClaimRole, "HR_MANAGER", StringComparison.OrdinalIgnoreCase) ||
               normalizedRoleName.Contains("HR MANAGER", StringComparison.Ordinal) ||
               normalizedRoleName.Contains("NHAN SU", StringComparison.Ordinal);
    }

    private static bool IsManagerStaff(string? claimRole, string? roleCode, string? roleName)
    {
        var normalizedRoleName = NormalizeRoleNameForMatch(roleName);
        var normalizedClaimRole = (claimRole ?? string.Empty).Trim();
        return string.Equals(roleCode, "MANAGER", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedClaimRole, "MANAGER", StringComparison.OrdinalIgnoreCase) ||
               normalizedRoleName.Contains("MANAGER", StringComparison.Ordinal) ||
               normalizedRoleName.Contains("QUAN LY", StringComparison.Ordinal);
    }

    private static string NormalizeRoleNameForMatch(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return string.Empty;
        }

        var normalized = TryFixUtf8Mojibake(roleName.Trim());
        var decomposed = normalized.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                buffer.Append(c);
            }
        }

        return buffer.ToString()
            .Replace('Đ', 'D')
            .Replace('đ', 'd')
            .ToUpperInvariant();
    }

    private static string TryFixUtf8Mojibake(string value)
    {
        if (value.IndexOfAny(['\u00C3', '\u00C2', '\u00D0', '\u00C4']) < 0)
        {
            return value;
        }

        var decoded = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(value));
        return decoded.Contains('\uFFFD') ? value : decoded;
    }

    private static bool IsAdminStaff(string? claimRole, string? roleCode, string? roleName)
    {
        var normalizedClaimRole = (claimRole ?? string.Empty).Trim();
        var roleNameUpper = (roleName ?? string.Empty).Trim().ToUpperInvariant();
        return string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedClaimRole, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
               roleNameUpper.Contains("ADMIN", StringComparison.OrdinalIgnoreCase);
    }
}
