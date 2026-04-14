using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class StandardServiceService : GenericService<StandardService>, IStandardServiceService
    {
        public StandardServiceService(IStandardServiceRepository repository) : base(repository)
        {
        }
    }
}
