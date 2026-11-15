using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Common;
using MyGarage.Application.Exceptions;
using MyGarage.Data;
using MyGarage.Interfaces;
using TableConfig = MyGarage.Services.AccountingCrudTableConfig;

namespace MyGarage.Services;

public sealed class AccountingCrudService : IAccountingCrudService
{
    private readonly IGarageContext _db;
    private readonly ConcurrentDictionary<string, HashSet<string>> _tableColumnsCache = new(StringComparer.OrdinalIgnoreCase);

    public AccountingCrudService(IGarageContext db)
    {
        _db = db;
    }

    public bool TryGetConfig(string key, out TableConfig config)
    {
        return AccountingCrudSchemaHelper.TryGetConfig(key, out config);
    }

    public async Task<bool> TableExistsAsync(TableConfig config)
    {
        var exists = await ScalarAsync<int?>(
            "SELECT TOP 1 1 FROM sys.objects WHERE object_id = OBJECT_ID(@name) AND type = 'U';",
            [new QueryParam("@name", config.TableName)]);
        return exists.HasValue;
    }

    public async Task<bool> ViewExistsAsync(string schemaViewName)
    {
        var exists = await ScalarAsync<int?>(
            "SELECT TOP 1 1 FROM sys.objects WHERE object_id = OBJECT_ID(@name) AND type = 'V';",
            [new QueryParam("@name", schemaViewName)]);
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
                [new QueryParam("@limit", effectiveLimit), new QueryParam("@branchId", branchId!.Value)]);
        }

        var sql = $"SELECT TOP (@limit) * FROM {config.TableName} {orderBy};";
        return await QueryAsync(sql, [new QueryParam("@limit", effectiveLimit)]);
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
                [new QueryParam("@id", id), new QueryParam("@branchId", branchId!.Value)])
            : await QueryAsync(
                $"SELECT TOP 1 * FROM {config.TableName} WHERE id = @id;",
                [new QueryParam("@id", id)]);
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
        var values = AccountingCrudSchemaHelper.ExtractWritableValues(payload, config, columns);
        var scopedByBranch = branchId.HasValue && columns.Contains("branch_id");
        if (scopedByBranch)
        {
            var incomingBranch = AccountingCrudSchemaHelper.TryExtractBranchId(values);
            if (incomingBranch.HasValue && incomingBranch.Value != branchId!.Value)
            {
                throw new InvalidOperationException("Cannot create data for another branch.");
            }
            if (!incomingBranch.HasValue)
            {
                values.Add(new AccountingCrudSchemaHelper.WritableValue("branch_id", branchId!.Value));
            }
        }

        if (!values.Any())
        {
            throw new InvalidOperationException("No writable fields were provided.");
        }

        var colNames = values.Select(v => v.ColumnName).ToArray();
        var parameters = new List<QueryParam>();
        var paramNames = new List<string>();
        for (var i = 0; i < values.Count; i++)
        {
            var paramName = $"@p{i}";
            paramNames.Add(paramName);
            parameters.Add(new QueryParam(paramName, values[i].Value));
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
        var values = AccountingCrudSchemaHelper.ExtractWritableValues(payload, config, columns);
        var scopedByBranch = branchId.HasValue && columns.Contains("branch_id");
        if (scopedByBranch)
        {
            var existingScopedRow = await GetByIdAsync(tableKey, id, branchId);
            if (existingScopedRow is null)
            {
                return null;
            }

            var incomingBranch = AccountingCrudSchemaHelper.TryExtractBranchId(values);
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
        var parameters = new List<QueryParam>();
        for (var i = 0; i < values.Count; i++)
        {
            var paramName = $"@p{i}";
            setClauses.Add($"{values[i].ColumnName} = {paramName}");
            parameters.Add(new QueryParam(paramName, values[i].Value));
        }

        if (config.HasUpdatedAt && columns.Contains("updated_at"))
        {
            setClauses.Add("updated_at = SYSDATETIME()");
        }

        parameters.Add(new QueryParam("@id", id));
        if (scopedByBranch)
        {
            parameters.Add(new QueryParam("@branchId", branchId!.Value));
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
                [new QueryParam("@id", id), new QueryParam("@branchId", branchId!.Value)])
            : await ExecuteAsync(
                $"DELETE FROM {config.TableName} WHERE id = @id;",
                [new QueryParam("@id", id)]);
        return affected > 0;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(string sql, IEnumerable<QueryParam>? parameters = null)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            AccountingCrudSchemaHelper.BindTransaction(command, _db);
            AccountingCrudSchemaHelper.AddParameters(command, parameters);

            var results = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var key = AccountingCrudSchemaHelper.ToCamelCase(reader.GetName(i));
                    var value = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                    row[key] = AccountingCrudSchemaHelper.NormalizeReadValue(value);
                }
                results.Add(row);
            }

            return results;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new DuplicateKeyException(ex.Message, ex);
        }
        catch (SqlException ex)
        {
            throw new DatabaseOperationException(ex.Message, ex);
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<int> ExecuteAsync(string sql, IEnumerable<QueryParam>? parameters = null)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            AccountingCrudSchemaHelper.BindTransaction(command, _db);
            AccountingCrudSchemaHelper.AddParameters(command, parameters);
            return await command.ExecuteNonQueryAsync();
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new DuplicateKeyException(ex.Message, ex);
        }
        catch (SqlException ex)
        {
            throw new DatabaseOperationException(ex.Message, ex);
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<T?> ScalarAsync<T>(string sql, IEnumerable<QueryParam>? parameters = null)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            AccountingCrudSchemaHelper.BindTransaction(command, _db);
            AccountingCrudSchemaHelper.AddParameters(command, parameters);

            var value = await command.ExecuteScalarAsync();
            if (value is null || value == DBNull.Value)
            {
                return default;
            }
            return (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new DuplicateKeyException(ex.Message, ex);
        }
        catch (SqlException ex)
        {
            throw new DatabaseOperationException(ex.Message, ex);
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }
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
            [new QueryParam("@name", fullTableName)]);
        var set = rows
            .Select(x => x.TryGetValue("name", out var name) ? Convert.ToString(name) : null)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _tableColumnsCache[fullTableName] = set;
        return set;
    }
}
