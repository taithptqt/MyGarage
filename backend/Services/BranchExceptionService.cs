using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class BranchExceptionService : GenericService<BranchException>, IBranchExceptionService
    {
        public BranchExceptionService(IBranchExceptionRepository repository) : base(repository)
        {
        }
    }
}
