using System.Collections.Concurrent;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Data;
using MyGarage.Interfaces;
using TableConfig = MyGarage.Services.AccountingCrudTableConfig;

namespace MyGarage.Services;

public sealed class AccountingCrudService : IAccountingCrudService
{
    private readonly IGarageContext _db;
    private readonly ConcurrentDictionary<string, HashSet<string>> _tableColumnsCache = new(StringComparer.OrdinalIgnoreCase);

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

    public AccountingCrudService(IGarageContext db)
    {
        _db = db;
    }

    public bool TryGetConfig(string key, out TableConfig config)
    {
        return TableConfigs.TryGetValue(key, out config!);
    }

    public async Task<bool> TableExistsAsync(TableConfig config)
    {
        var exists = await ScalarAsync<int?>(
            "SELECT TOP 1 1 FROM sys.objects WHERE object_id = OBJECT_ID(@name) AND type = 'U';",
            [new SqlParameter("@name", config.TableName)]);
        return exists.HasValue;
    }

    public async Task<bool> ViewExistsAsync(string schemaViewName)
    {
        var exists = await ScalarAsync<int?>(
            "SELECT TOP 1 1 FROM sys.objects WHERE object_id = OBJECT_ID(@name) AND type = 'V';",
            [new SqlParameter("@name", schemaViewName)]);
        return exists.HasValue;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetAllAsync(string tableKey, int limit = 500, long? branchId = null)
    {
        if (!TryGetConfig(tableKey, out var config))
        {
            throw new KeyNotFoundException($"Unsupported table key: {tableKey}");
        }
        if (!await TableExistsAsync(config))
        {
            throw new InvalidOperationException($"Table {config.TableName} does not exist.");
        }

        var columns = await GetColumnsAsync(config.TableName);
        var orderBy = columns.Contains("id") ? "ORDER BY id DESC" : string.Empty;
        var effectiveLimit = Math.Max(1, Math.Min(limit, 2000));
        var scopedByBranch = branchId.HasValue && columns.Contains("branch_id");
        if (scopedByBranch)
        {
            var sqlScoped = $"SELECT TOP (@limit) * FROM {config.TableName} WHERE branch_id = @branchId {orderBy};";
            return await QueryAsync(
                sqlScoped,
                [new SqlParameter("@limit", effectiveLimit), new SqlParameter("@branchId", branchId!.Value)]);
        }

        var sql = $"SELECT TOP (@limit) * FROM {config.TableName} {orderBy};";
        return await QueryAsync(sql, [new SqlParameter("@limit", effectiveLimit)]);
    }

    public async Task<Dictionary<string, object?>?> GetByIdAsync(string tableKey, long id, long? branchId = null)
    {
        if (!TryGetConfig(tableKey, out var config))
        {
            throw new KeyNotFoundException($"Unsupported table key: {tableKey}");
        }
        if (!await TableExistsAsync(config))
        {
            throw new InvalidOperationException($"Table {config.TableName} does not exist.");
        }
        var columns = await GetColumnsAsync(config.TableName);
        var scopedByBranch = branchId.HasValue && columns.Contains("branch_id");
        var rows = scopedByBranch
            ? await QueryAsync(
                $"SELECT TOP 1 * FROM {config.TableName} WHERE id = @id AND branch_id = @branchId;",
                [new SqlParameter("@id", id), new SqlParameter("@branchId", branchId!.Value)])
            : await QueryAsync(
                $"SELECT TOP 1 * FROM {config.TableName} WHERE id = @id;",
                [new SqlParameter("@id", id)]);
        return rows.FirstOrDefault();
    }

    public async Task<Dictionary<string, object?>?> InsertAsync(string tableKey, JsonElement payload, long? branchId = null)
    {
        if (!TryGetConfig(tableKey, out var config))
        {
            throw new KeyNotFoundException($"Unsupported table key: {tableKey}");
        }
        if (!await TableExistsAsync(config))
        {
            throw new InvalidOperationException($"Table {config.TableName} does not exist.");
        }
        var columns = await GetColumnsAsync(config.TableName);
        var values = ExtractWritableValues(payload, config, columns);
        var scopedByBranch = branchId.HasValue && columns.Contains("branch_id");
        if (scopedByBranch)
        {
            var incomingBranch = TryExtractBranchId(values);
            if (incomingBranch.HasValue && incomingBranch.Value != branchId!.Value)
            {
                throw new InvalidOperationException("Cannot create data for another branch.");
            }
            if (!incomingBranch.HasValue)
            {
                values.Add(new WritableValue("branch_id", branchId!.Value));
            }
        }

        if (!values.Any())
        {
            throw new InvalidOperationException("No writable fields were provided.");
        }

        var colNames = values.Select(v => v.ColumnName).ToArray();
        var parameters = new List<SqlParameter>();
        var paramNames = new List<string>();
        for (var i = 0; i < values.Count; i++)
        {
            var paramName = $"@p{i}";
            paramNames.Add(paramName);
            parameters.Add(new SqlParameter(paramName, values[i].Value ?? DBNull.Value));
        }

        var sql = $"""
                   DECLARE @inserted TABLE (id BIGINT);
                   INSERT INTO {config.TableName} ({string.Join(", ", colNames)})
                   OUTPUT INSERTED.id INTO @inserted(id)
                   VALUES ({string.Join(", ", paramNames)});
                   SELECT TOP 1 id FROM @inserted;
                   """;
        var id = await ScalarAsync<long?>(sql, parameters);
        if (!id.HasValue) return null;
        return await GetByIdAsync(tableKey, id.Value, branchId);
    }

    public async Task<Dictionary<string, object?>?> UpdateAsync(string tableKey, long id, JsonElement payload, long? branchId = null)
    {
        if (!TryGetConfig(tableKey, out var config))
        {
            throw new KeyNotFoundException($"Unsupported table key: {tableKey}");
        }
        if (!await TableExistsAsync(config))
        {
            throw new InvalidOperationException($"Table {config.TableName} does not exist.");
        }
        var columns = await GetColumnsAsync(config.TableName);
        var values = ExtractWritableValues(payload, config, columns);
        var scopedByBranch = branchId.HasValue && columns.Contains("branch_id");
        if (scopedByBranch)
        {
            var existingScopedRow = await GetByIdAsync(tableKey, id, branchId);
            if (existingScopedRow is null)
            {
                return null;
            }

            var incomingBranch = TryExtractBranchId(values);
            if (incomingBranch.HasValue && incomingBranch.Value != branchId!.Value)
            {
                throw new InvalidOperationException("Cannot move data to another branch.");
            }

            values = values
                .Where(v => !v.ColumnName.Equals("branch_id", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        if (!values.Any() && !(config.HasUpdatedAt && columns.Contains("updated_at")))
        {
            throw new InvalidOperationException("No writable fields were provided.");
        }

        var setClauses = new List<string>();
        var parameters = new List<SqlParameter>();
        for (var i = 0; i < values.Count; i++)
        {
            var paramName = $"@p{i}";
            setClauses.Add($"{values[i].ColumnName} = {paramName}");
            parameters.Add(new SqlParameter(paramName, values[i].Value ?? DBNull.Value));
        }

        if (config.HasUpdatedAt && columns.Contains("updated_at"))
        {
            setClauses.Add("updated_at = SYSDATETIME()");
        }

        parameters.Add(new SqlParameter("@id", id));
        if (scopedByBranch)
        {
            parameters.Add(new SqlParameter("@branchId", branchId!.Value));
        }
        var whereClause = scopedByBranch ? "id = @id AND branch_id = @branchId" : "id = @id";
        var sql = $"UPDATE {config.TableName} SET {string.Join(", ", setClauses)} WHERE {whereClause};";
        await ExecuteAsync(sql, parameters);
        return await GetByIdAsync(tableKey, id, branchId);
    }

    public async Task<bool> DeleteAsync(string tableKey, long id, long? branchId = null)
    {
        if (!TryGetConfig(tableKey, out var config))
        {
            throw new KeyNotFoundException($"Unsupported table key: {tableKey}");
        }
        if (!await TableExistsAsync(config))
        {
            throw new InvalidOperationException($"Table {config.TableName} does not exist.");
        }
        var columns = await GetColumnsAsync(config.TableName);
        var scopedByBranch = branchId.HasValue && columns.Contains("branch_id");
        var affected = scopedByBranch
            ? await ExecuteAsync(
                $"DELETE FROM {config.TableName} WHERE id = @id AND branch_id = @branchId;",
                [new SqlParameter("@id", id), new SqlParameter("@branchId", branchId!.Value)])
            : await ExecuteAsync(
                $"DELETE FROM {config.TableName} WHERE id = @id;",
                [new SqlParameter("@id", id)]);
        return affected > 0;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(string sql, IEnumerable<SqlParameter>? parameters = null)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            BindTransaction(command);
            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    command.Parameters.Add(p);
                }
            }

            var results = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var key = ToCamelCase(reader.GetName(i));
                    var value = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                    row[key] = NormalizeReadValue(value);
                }
                results.Add(row);
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<int> ExecuteAsync(string sql, IEnumerable<SqlParameter>? parameters = null)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            BindTransaction(command);
            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    command.Parameters.Add(p);
                }
            }
            return await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<T?> ScalarAsync<T>(string sql, IEnumerable<SqlParameter>? parameters = null)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            BindTransaction(command);
            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    command.Parameters.Add(p);
                }
            }

            var value = await command.ExecuteScalarAsync();
            if (value is null || value == DBNull.Value)
            {
                return default;
            }
            return (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private void BindTransaction(IDbCommand command)
    {
        var tx = _db.Database.CurrentTransaction?.GetDbTransaction();
        if (tx != null)
        {
            command.Transaction = tx;
        }
    }

    private async Task<HashSet<string>> GetColumnsAsync(string fullTableName)
    {
        if (_tableColumnsCache.TryGetValue(fullTableName, out var cached))
        {
            return cached;
        }

        var rows = await QueryAsync(
            "SELECT c.name FROM sys.columns c WHERE c.object_id = OBJECT_ID(@name);",
            [new SqlParameter("@name", fullTableName)]);
        var set = rows
            .Select(x => x.TryGetValue("name", out var name) ? Convert.ToString(name) : null)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _tableColumnsCache[fullTableName] = set;
        return set;
    }

    private static List<WritableValue> ExtractWritableValues(JsonElement payload, TableConfig config, HashSet<string> realColumns)
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

    private static long? TryExtractBranchId(IEnumerable<WritableValue> values)
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

    private static object? NormalizeReadValue(object? value)
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

    private static string ToCamelCase(string key)
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

    private sealed record WritableValue(string ColumnName, object? Value);
}


