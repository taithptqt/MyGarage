using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IAccountingTableCommandService
    {
        Task<Dictionary<string, object?>?> InsertAsync(string tableKey, JsonElement payload, long? branchId = null);
        Task<Dictionary<string, object?>?> UpdateAsync(string tableKey, long id, JsonElement payload, long? branchId = null);
        Task<bool> DeleteAsync(string tableKey, long id, long? branchId = null);
    }
}
