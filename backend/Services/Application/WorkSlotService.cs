using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class WorkSlotService : GenericService<WorkSlot>, IWorkSlotService
    {
        public WorkSlotService(IWorkSlotRepository repository) : base(repository)
        {
        }
    }
}
