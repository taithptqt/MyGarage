using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class AiForecastRepository : GenericRepository<AiForecast>, IAiForecastRepository
    {
        public AiForecastRepository(IGarageContext context) : base(context)
        {
        }
    }
}

