using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceOrderSnapshotService : GenericService<ServiceOrderSnapshot>, IServiceOrderSnapshotService
    {
        public ServiceOrderSnapshotService(IServiceOrderSnapshotRepository repository) : base(repository)
        {
        }
    }
}
