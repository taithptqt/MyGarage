using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ServiceRefundRepository : GenericRepository<ServiceRefund>, IServiceRefundRepository
    {
        public ServiceRefundRepository(IGarageContext context) : base(context)
        {
        }
    }
}

