using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class BranchService : GenericService<Branch>, IBranchService
    {
        public BranchService(IBranchRepository repository) : base(repository)
        {
        }
    }
}
