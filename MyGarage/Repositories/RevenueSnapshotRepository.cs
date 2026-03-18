using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class RevenueSnapshotRepository : GenericRepository<RevenueSnapshot>, IRevenueSnapshotRepository
    {
        public RevenueSnapshotRepository(GarageContext context) : base(context)
        {
        }
    }
}
