using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CommissionRuleService : GenericService<CommissionRule>, ICommissionRuleService
    {
        public CommissionRuleService(ICommissionRuleRepository repository) : base(repository)
        {
        }
    }
}
