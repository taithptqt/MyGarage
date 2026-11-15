using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IAccountingTableQueryService
    {
        Task<IReadOnlyList<Dictionary<string, object?>>> GetAllAsync(string tableKey, int limit = 500, long? branchId = null);
        Task<Dictionary<string, object?>?> GetByIdAsync(string tableKey, long id, long? branchId = null);
    }
}
