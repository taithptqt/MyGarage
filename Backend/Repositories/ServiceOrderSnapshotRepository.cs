using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ServiceOrderSnapshotRepository : GenericRepository<ServiceOrderSnapshot>, IServiceOrderSnapshotRepository
    {
        public ServiceOrderSnapshotRepository(GarageContext context) : base(context)
        {
        }
    }
}
