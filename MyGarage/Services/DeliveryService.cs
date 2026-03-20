using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class DeliveryService : GenericService<Delivery>, IDeliveryService
    {
        public DeliveryService(IDeliveryRepository repository) : base(repository)
        {
        }
    }
}
