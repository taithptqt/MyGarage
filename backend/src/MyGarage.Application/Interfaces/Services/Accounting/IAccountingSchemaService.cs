using System.Threading.Tasks;
using MyGarage.Services;

namespace MyGarage.Interfaces
{
    public interface IAccountingSchemaService
    {
        bool TryGetConfig(string key, out AccountingCrudTableConfig config);
        Task<bool> TableExistsAsync(AccountingCrudTableConfig config);
        Task<bool> ViewExistsAsync(string schemaViewName);
    }
}
