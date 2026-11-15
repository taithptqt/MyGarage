using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Common;
using MyGarage.Interfaces;
using TableConfig = MyGarage.Services.AccountingCrudTableConfig;

namespace MyGarage.Services;

internal static class AccountingCrudSchemaHelper
{
    private static readonly Regex CamelSplitRegex = new("(?<!^)([A-Z])", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, TableConfig> TableConfigs =
        new Dictionary<string, TableConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["accounts-payable"] = new(
                "dbo.accounts_payable",
                [
                    "supplier_invoice_id","supplier_id","branch_id","supplier_invoice_no","supplier_invoice_date",
                    "due_date","original_amount","paid_amount","outstanding_amount","status","purchase_order_id","note"
                ]),
            ["supplier-invoices"] = new(
                "dbo.supplier_invoices",
                [
                    "supplier_id","purchase_order_id","invoice_no","invoice_date","tax_code","sub_total",
                    "tax_amount","total_amount","attachment_url","status","created_by"
                ]),
            ["supplier-invoice-details"] = new(
                "dbo.supplier_invoice_details",
                [
                    "supplier_invoice_id","purchase_order_item_id","product_id","item_name","quantity","unit",
                    "unit_cost","sub_total","tax_rate","tax_amount","final_amount","note"
                ]),
            ["supplier-payments"] = new(
                "dbo.supplier_payments",
                [
                    "accounts_payable_id","supplier_id","branch_id","payment_method_id","paid_amount","paid_at",
                    "transaction_code","note","created_by"
                ], HasUpdatedAt: false),
            ["supplier-accounting-profiles"] = new(
                "dbo.supplier_accounting_profiles",
                [
                    "supplier_id","payable_account_id","purchase_tax_account_id","prepaid_account_id","note"
                ]),
            ["accounting-periods"] = new(
                "dbo.accounting_periods",
                [
                    "branch_id","period_code","start_date","end_date","status","closed_by","closed_at","note"
                ]),
            ["chart-of-accounts"] = new(
                "dbo.chart_of_accounts",
                [
                    "account_code","account_name","parent_account_id","account_type","allows_posting","is_active"
                ]),
            ["journal-entries"] = new(
                "dbo.journal_entries",
                [
                    "entry_no","entry_date","period_id","source_type","source_id","description","status",
                    "posted_by","posted_at","reversed_entry_id"
                ]),
            ["journal-entry-lines"] = new(
                "dbo.journal_entry_lines",
                [
                    "journal_entry_id","account_id","branch_id","customer_id","supplier_id","staff_id","invoice_id",
                    "payment_id","supplier_invoice_id","supplier_payment_id","reference_no","debit_amount",
                    "credit_amount","note"
                ], HasUpdatedAt: false),
            ["finance-approval-requests"] = new(
                "dbo.finance_approval_requests",
                [
                    "request_type","invoice_id","payment_id","supplier_invoice_id","supplier_payment_id",
                    "journal_entry_id","payroll_run_id","tax_filing_id","requested_by","approver_id","status",
                    "risk_level","amount","reason","reject_reason","request_payload"
                ]),
            ["approval-policies"] = new(
                "dbo.approval_policies",
                [
                    "policy_code","policy_name","trigger_type","threshold_amount","required_approver_role","is_active"
                ]),
            ["bank-accounts"] = new(
                "dbo.bank_accounts",
                [
                    "branch_id","bank_name","account_number","account_holder","is_active"
                ]),
            ["bank-transactions"] = new(
                "dbo.bank_transactions",
                [
                    "bank_account_id","txn_date","txn_ref","description","in_amount","out_amount","balance_after",
                    "payment_id","supplier_payment_id","status"
                ], HasUpdatedAt: false),
            ["bank-reconciliations"] = new(
                "dbo.bank_reconciliations",
                [
                    "bank_account_id","period_from","period_to","opening_balance","closing_balance_statement",
                    "closing_balance_system","difference","status","prepared_by","approved_by"
                ]),
            ["staff-contracts"] = new(
                "dbo.staff_contracts",
                [
                    "staff_id","contract_type","effective_from","effective_to","base_salary","insurance_salary_base",
                    "allowance_amount","pay_frequency","bank_account_name","bank_account_number","bank_name",
                    "tax_code","status"
                ]),
            ["staff-salary-profiles"] = new(
                "dbo.staff_salary_profiles",
                [
                    "staff_id","branch_id","base_salary","effective_from","effective_to","status","note","updated_by"
                ]),
            ["payroll-periods"] = new(
                "dbo.payroll_periods",
                [
                    "period_code","start_date","end_date","payroll_date","status"
                ]),
            ["payroll-runs"] = new(
                "dbo.payroll_runs",
                [
                    "payroll_period_id","branch_id","run_no","status","calculated_by","calculated_at",
                    "approved_by","approved_at","paid_at"
                ]),
            ["payroll-items"] = new(
                "dbo.payroll_items",
                [
                    "payroll_run_id","staff_id","base_salary","attendance_deduction","overtime_amount","allowance_amount",
                    "commission_amount","bonus_amount","penalty_amount","advance_deduction","loan_deduction",
                    "insurance_employee_amount","insurance_company_amount","pit_amount","other_deduction",
                    "net_pay","status"
                ]),
            ["payslips"] = new(
                "dbo.payslips",
                [
                    "payroll_item_id","slip_no","issued_at","issued_by","file_url","viewed_at"
                ], HasUpdatedAt: false),
            ["salary-advances"] = new(
                "dbo.salary_advances",
                [
                    "staff_id","amount","request_date","approved_by","approved_at","deduct_from_payroll_period_id",
                    "status","note"
                ]),
            ["employee-loans"] = new(
                "dbo.employee_loans",
                [
                    "staff_id","loan_amount","outstanding_amount","monthly_deduction","start_payroll_period_id",
                    "status","note"
                ]),
            ["attendance-adjustments"] = new(
                "dbo.attendance_adjustments",
                [
                    "staff_id","work_date","adjustment_type","minutes","amount_impact","reason","approved_by","status"
                ]),
            ["overtime-entries"] = new(
                "dbo.overtime_entries",
                [
                    "staff_id","work_date","overtime_type","hours","rate_multiplier","amount","approved_by","status"
                ]),
            ["pit-profiles"] = new(
                "dbo.pit_profiles",
                [
                    "staff_id","tax_code","resident_type","dependent_count","effective_from","effective_to"
                ]),
            ["employee-dependents"] = new(
                "dbo.employee_dependents",
                [
                    "staff_id","full_name","relationship","dob","tax_registration_date","effective_from","effective_to"
                ]),
            ["tax-filings"] = new(
                "dbo.tax_filings",
                [
                    "tax_type","period_code","from_date","to_date","status","prepared_by","approved_by",
                    "submitted_at","attachment_url","note"
                ]),
            ["legal-documents"] = new(
                "dbo.legal_documents",
                [
                    "doc_type","reference_no","title","related_entity_type","related_entity_id","issued_date",
                    "file_url","uploaded_by"
                ], HasUpdatedAt: false),
            ["document-attachments"] = new(
                "dbo.document_attachments",
                [
                    "entity_type","entity_id","file_name","file_url","uploaded_by","uploaded_at"
                ], HasUpdatedAt: false)
        };

    internal static bool TryGetConfig(string key, out TableConfig config)
    {
        return TableConfigs.TryGetValue(key, out config!);
    }

    internal static void AddParameters(IDbCommand command, IEnumerable<QueryParam>? parameters)
    {
        if (parameters == null)
        {
            return;
        }

        foreach (var p in parameters)
        {
            command.Parameters.Add(new SqlParameter(p.Name, p.Value ?? DBNull.Value));
        }
    }

    internal static void BindTransaction(IDbCommand command, IGarageContext db)
    {
        var tx = db.Database.CurrentTransaction?.GetDbTransaction();
        if (tx != null)
        {
            command.Transaction = tx;
        }
    }

    internal static List<WritableValue> ExtractWritableValues(JsonElement payload, TableConfig config, HashSet<string> realColumns)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Payload must be a JSON object.");
        }

        var result = new List<WritableValue>();
        foreach (var prop in payload.EnumerateObject())
        {
            var snake = ToSnakeCase(prop.Name);
            if (!config.WritableColumns.Contains(snake))
            {
                continue;
            }
            if (!realColumns.Contains(snake))
            {
                continue;
            }
            result.Add(new WritableValue(snake, ConvertJsonValue(prop.Value)));
        }
        return result;
    }

    private static object? ConvertJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => DBNull.Value,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var i) => i,
            JsonValueKind.Number when value.TryGetDecimal(out var d) => d,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array or JsonValueKind.Object => value.GetRawText(),
            _ => value.ToString()
        };
    }

    internal static long? TryExtractBranchId(IEnumerable<WritableValue> values)
    {
        var branchField = values.FirstOrDefault(v => v.ColumnName.Equals("branch_id", StringComparison.OrdinalIgnoreCase));
        if (branchField is null) return null;
        return branchField.Value switch
        {
            null => null,
            DBNull => null,
            long x => x,
            int x => x,
            short x => x,
            byte x => x,
            decimal x => Convert.ToInt64(x),
            double x => Convert.ToInt64(x),
            float x => Convert.ToInt64(x),
            string x when long.TryParse(x, out var parsed) => parsed,
            _ => Convert.ToInt64(branchField.Value)
        };
    }

    internal static object? NormalizeReadValue(object? value)
    {
        return value is string text ? TryFixUtf8Mojibake(text) : value;
    }

    private static string TryFixUtf8Mojibake(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.IndexOfAny(['\u00C3', '\u00C2', '\u00D0', '\u00C4']) < 0)
        {
            return value;
        }

        var decoded = Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(value));
        return decoded.Contains('\uFFFD') ? value : decoded;
    }

    private static string ToSnakeCase(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return key;
        if (key.Contains('_')) return key.ToLowerInvariant();
        return CamelSplitRegex.Replace(key, "_$1").ToLowerInvariant();
    }

    internal static string ToCamelCase(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return key;
        if (key.Contains('_'))
        {
            var parts = key.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return key;
            return parts[0].ToLowerInvariant() + string.Concat(parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
        }
        return char.ToLowerInvariant(key[0]) + key[1..];
    }

    internal sealed record WritableValue(string ColumnName, object? Value);
}
