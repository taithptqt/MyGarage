using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ServiceOrderDiscountRepository : GenericRepository<ServiceOrderDiscount>, IServiceOrderDiscountRepository
    {
        public ServiceOrderDiscountRepository(IGarageContext context) : base(context)
        {
        }
    }
}

