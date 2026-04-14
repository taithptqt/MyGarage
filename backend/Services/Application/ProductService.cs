using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ProductService : GenericService<Product>, IProductService
    {
        private readonly IGarageContext _context;

        public ProductService(IProductRepository repository, IGarageContext context) : base(repository)
        {
            _context = context;
        }

        public override async Task<IEnumerable<Product>> GetAllAsync()
        {
            return await _context.Products
                .Where(product => product.IsDeleted != true)
                .ToListAsync();
        }

        public override async Task<Product?> GetByIdAsync(object id)
        {
            if (!TryResolveProductId(id, out var productId))
            {
                return null;
            }

            return await _context.Products
                .FirstOrDefaultAsync(product => product.Id == productId && product.IsDeleted != true);
        }

        public override async Task<bool> DeleteAsync(object id)
        {
            if (!TryResolveProductId(id, out var productId))
            {
                return false;
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(item => item.Id == productId && item.IsDeleted != true);
            if (product == null)
            {
                return false;
            }

            product.IsDeleted = true;
            product.IsActive = false;
            product.DeletedAt = VietnamTime.Now;
            product.UpdatedAt = VietnamTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        private static bool TryResolveProductId(object id, out long productId)
        {
            switch (id)
            {
                case long value:
                    productId = value;
                    return true;
                case int value:
                    productId = value;
                    return true;
                case string text when long.TryParse(text, out var parsed):
                    productId = parsed;
                    return true;
                default:
                    productId = default;
                    return false;
            }
        }
    }
}
