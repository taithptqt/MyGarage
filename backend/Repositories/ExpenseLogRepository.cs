using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ExpenseLogRepository : GenericRepository<ExpenseLog>, IExpenseLogRepository
    {
        public ExpenseLogRepository(IGarageContext context) : base(context)
        {
        }
    }
}

