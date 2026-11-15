using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RetailOrderService : GenericService<RetailOrder>, IRetailOrderService
    {
        public RetailOrderService(IRetailOrderRepository repository) : base(repository)
        {
        }
    }
}
