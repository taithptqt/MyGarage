using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Application.Common;

namespace MyGarage.Interfaces
{
    public interface IAccountingSqlCommandService
    {
        Task<int> ExecuteAsync(string sql, IEnumerable<QueryParam>? parameters = null);
    }
}
