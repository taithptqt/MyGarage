using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class GatePassService : GenericService<GatePass>, IGatePassService
    {
        public GatePassService(IGatePassRepository repository) : base(repository)
        {
        }
    }
}
