using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CategoryService : GenericService<Category>, ICategoryService
    {
        public CategoryService(ICategoryRepository repository) : base(repository)
        {
        }
    }
}
