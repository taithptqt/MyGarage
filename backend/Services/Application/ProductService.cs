using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ProductService : GenericService<Product>, IProductService
    {
        public ProductService(IProductRepository repository) : base(repository)
        {
        }
    }
}
