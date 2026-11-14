using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class PackageService : GenericService<Package>, IPackageService
    {
        private readonly IPackageRepository _packageRepository;

        public PackageService(IPackageRepository repository) : base(repository)
        {
            _packageRepository = repository;
        }

        public Task<IEnumerable<Package>> GetAllWithItemsAsync()
        {
            return _packageRepository.GetAllWithItemsAsync();
        }

        public Task<Package?> GetByIdWithItemsAsync(long id)
        {
            return _packageRepository.GetByIdWithItemsAsync(id);
        }
    }
}
