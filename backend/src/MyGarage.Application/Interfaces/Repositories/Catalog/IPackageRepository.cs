using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IPackageRepository : IGenericRepository<Package>
    {
        Task<IEnumerable<Package>> GetAllWithItemsAsync();
        Task<Package?> GetByIdWithItemsAsync(long id);
    }
}
