using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Repositories
{
    public class TeamRepository : GenericRepository<Team>, ITeamRepository
    {
        public TeamRepository(IGarageContext context) : base(context)
        {
        }
    }
}
