using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class TeamService : GenericService<Team>, ITeamService
    {
        public TeamService(ITeamRepository repository) : base(repository)
        {
        }
    }
}
