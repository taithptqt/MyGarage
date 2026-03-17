using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class StockTransactionRepository : GenericRepository<StockTransaction>, IStockTransactionRepository
    {
        public StockTransactionRepository(GarageContext context) : base(context)
        {
        }
    }
}
