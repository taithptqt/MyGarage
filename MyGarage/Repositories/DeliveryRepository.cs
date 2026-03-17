using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class DeliveryRepository : GenericRepository<Delivery>, IDeliveryRepository
    {
        public DeliveryRepository(GarageContext context) : base(context)
        {
        }
    }
}
