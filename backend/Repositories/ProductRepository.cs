using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ProductRepository : GenericRepository<Product>, IProductRepository
    {
        public ProductRepository(IGarageContext context) : base(context)
        {
        }
    }
}

