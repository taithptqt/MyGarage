using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class BayService : GenericService<Bay>, IBayService
    {
        public BayService(IBayRepository repository) : base(repository)
        {
        }
    }
}
