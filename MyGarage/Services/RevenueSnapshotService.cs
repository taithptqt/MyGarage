using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RevenueSnapshotService : GenericService<RevenueSnapshot>, IRevenueSnapshotService
    {
        public RevenueSnapshotService(IRevenueSnapshotRepository repository) : base(repository)
        {
        }
    }
}
