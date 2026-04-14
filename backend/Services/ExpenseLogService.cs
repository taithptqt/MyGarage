using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ExpenseLogService : GenericService<ExpenseLog>, IExpenseLogService
    {
        public ExpenseLogService(IExpenseLogRepository repository) : base(repository)
        {
        }
    }
}
