using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceOrderDiscountService : GenericService<ServiceOrderDiscount>, IServiceOrderDiscountService
    {
        public ServiceOrderDiscountService(IServiceOrderDiscountRepository repository) : base(repository)
        {
        }
    }
}
