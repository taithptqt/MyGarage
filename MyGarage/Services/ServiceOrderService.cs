using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceOrderService : GenericService<ServiceOrder>, IServiceOrderService
    {
        public ServiceOrderService(IServiceOrderRepository repository) : base(repository)
        {
        }
    }
}
