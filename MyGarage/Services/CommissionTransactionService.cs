using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CommissionTransactionService : GenericService<CommissionTransaction>, ICommissionTransactionService
    {
        public CommissionTransactionService(ICommissionTransactionRepository repository) : base(repository)
        {
        }
    }
}
