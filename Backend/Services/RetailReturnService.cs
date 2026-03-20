using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RetailReturnService : GenericService<RetailReturn>, IRetailReturnService
    {
        public RetailReturnService(IRetailReturnRepository repository) : base(repository)
        {
        }
    }
}
