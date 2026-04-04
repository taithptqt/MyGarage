using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AiFraudMonitorRepository : GenericRepository<AiFraudMonitor>, IAiFraudMonitorRepository
    {
        public AiFraudMonitorRepository(IGarageContext context) : base(context)
        {
        }
    }
}

