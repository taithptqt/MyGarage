using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceCategoryService : GenericService<ServiceCategory>, IServiceCategoryService
    {
        public ServiceCategoryService(IServiceCategoryRepository repository) : base(repository)
        {
        }
    }
}
