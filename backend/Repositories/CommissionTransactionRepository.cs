using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class CommissionTransactionRepository : GenericRepository<CommissionTransaction>, ICommissionTransactionRepository
    {
        public CommissionTransactionRepository(GarageContext context) : base(context)
        {
        }
    }
}
