using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class WorkSlotRepository : GenericRepository<WorkSlot>, IWorkSlotRepository
    {
        public WorkSlotRepository(IGarageContext context) : base(context)
        {
        }
    }
}

