using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Application.Common;

namespace MyGarage.Interfaces
{
    public interface IAccountingSqlQueryService
    {
        Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(string sql, IEnumerable<QueryParam>? parameters = null);
        Task<T?> ScalarAsync<T>(string sql, IEnumerable<QueryParam>? parameters = null);
    }
}
