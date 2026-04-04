using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class StockTransactionService : GenericService<StockTransaction>, IStockTransactionService
    {
        public StockTransactionService(IStockTransactionRepository repository) : base(repository)
        {
        }
    }
}
