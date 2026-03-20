using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class SupplierService : GenericService<Supplier>, ISupplierService
    {
        public SupplierService(ISupplierRepository repository) : base(repository)
        {
        }
    }
}
