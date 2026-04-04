using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class BranchExceptionRepository : GenericRepository<BranchException>, IBranchExceptionRepository
    {
        public BranchExceptionRepository(IGarageContext context) : base(context)
        {
        }
    }
}

