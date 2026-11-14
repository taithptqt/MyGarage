using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyGarage.Services;

namespace MyGarage.Interfaces
{
    public interface IAccountingCrudService
    {
        bool TryGetConfig(string key, out AccountingCrudTableConfig config);
        Task<bool> TableExistsAsync(AccountingCrudTableConfig config);
        Task<bool> ViewExistsAsync(string schemaViewName);
        Task<IReadOnlyList<Dictionary<string, object?>>> GetAllAsync(string tableKey, int limit = 500, long? branchId = null);
        Task<Dictionary<string, object?>?> GetByIdAsync(string tableKey, long id, long? branchId = null);
        Task<Dictionary<string, object?>?> InsertAsync(string tableKey, JsonElement payload, long? branchId = null);
        Task<Dictionary<string, object?>?> UpdateAsync(string tableKey, long id, JsonElement payload, long? branchId = null);
        Task<bool> DeleteAsync(string tableKey, long id, long? branchId = null);
        Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(string sql, IEnumerable<SqlParameter>? parameters = null);
        Task<int> ExecuteAsync(string sql, IEnumerable<SqlParameter>? parameters = null);
        Task<T?> ScalarAsync<T>(string sql, IEnumerable<SqlParameter>? parameters = null);
    }
}
