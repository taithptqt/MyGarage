using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiFraudMonitorService : GenericService<AiFraudMonitor>, IAiFraudMonitorService
    {
        public AiFraudMonitorService(IAiFraudMonitorRepository repository) : base(repository)
        {
        }
    }
}
