using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RoleService : GenericService<Role>, IRoleService
    {
        public RoleService(IRoleRepository repository) : base(repository)
        {
        }
    }
}
