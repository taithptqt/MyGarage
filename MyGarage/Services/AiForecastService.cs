using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiForecastService : GenericService<AiForecast>, IAiForecastService
    {
        public AiForecastService(IAiForecastRepository repository) : base(repository)
        {
        }
    }
}
