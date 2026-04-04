using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class BayRepository : GenericRepository<Bay>, IBayRepository
    {
        public BayRepository(IGarageContext context) : base(context)
        {
        }
    }
}


