using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class CommissionRuleRepository : GenericRepository<CommissionRule>, ICommissionRuleRepository
    {
        public CommissionRuleRepository(GarageContext context) : base(context)
        {
        }
    }
}
