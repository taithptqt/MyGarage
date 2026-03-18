using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceOrderDetailService : GenericService<ServiceOrderDetail>, IServiceOrderDetailService
    {
        public ServiceOrderDetailService(IServiceOrderDetailRepository repository) : base(repository)
        {
        }
    }
}
