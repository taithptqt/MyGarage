using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceRefundService : GenericService<ServiceRefund>, IServiceRefundService
    {
        public ServiceRefundService(IServiceRefundRepository repository) : base(repository)
        {
        }
    }
}
