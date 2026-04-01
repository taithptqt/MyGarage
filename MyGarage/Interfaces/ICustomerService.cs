using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ICustomerService : IGenericService<Customer>
    {
        Task<IEnumerable<Customer>> GetByBranchIdAsync(long branchId);
    }
}
