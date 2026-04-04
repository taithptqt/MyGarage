using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ServiceOrderDetailRepository : GenericRepository<ServiceOrderDetail>, IServiceOrderDetailRepository
    {
        public ServiceOrderDetailRepository(IGarageContext context) : base(context)
        {
        }
    }
}

