using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class GatePassRepository : GenericRepository<GatePass>, IGatePassRepository
    {
        public GatePassRepository(GarageContext context) : base(context)
        {
        }
    }
}
