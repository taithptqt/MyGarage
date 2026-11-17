using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class ModelSpecificRuleRepository : GenericRepository<ModelSpecificRule>, IModelSpecificRuleRepository
    {
        public override Task<(IReadOnlyList<ModelSpecificRule> Items, int Total)> GetPageByIdAsync(int page, int pageSize)
        {
            return GetOrderedPageAsync(
                _dbSet.AsNoTracking().OrderBy(rule => rule.ModelId).ThenBy(rule => rule.ProductId), page, pageSize);
        }

        public ModelSpecificRuleRepository(IGarageContext context) : base(context)
        {
        }
    }
}

