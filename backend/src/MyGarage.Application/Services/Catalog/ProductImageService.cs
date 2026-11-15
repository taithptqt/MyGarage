using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ProductImageService : GenericService<ProductImage>, IProductImageService
    {
        public ProductImageService(IProductImageRepository repository) : base(repository)
        {
        }
    }
}
