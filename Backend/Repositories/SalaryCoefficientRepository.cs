using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Repositories
{
    public class SalaryCoefficientRepository : GenericRepository<SalaryCoefficient>, ISalaryCoefficientRepository
    {
        public SalaryCoefficientRepository(GarageContext context) : base(context)
        {
        }

        public async Task<IEnumerable<SalaryCoefficient>> GetAllConfiguredAsync()
        {
            return await _context.SalaryCoefficients
                .AsNoTracking()
                .Where(item =>
                    item.IsDeleted != true &&
                    item.YearsOfService.HasValue)
                .OrderBy(item => item.YearsOfService)
                .ToListAsync();
        }

        public async Task<SalaryCoefficient?> GetConfiguredByIdAsync(long id)
        {
            return await _context.SalaryCoefficients
                .FirstOrDefaultAsync(item =>
                    item.Id == id &&
                    item.IsDeleted != true &&
                    item.YearsOfService.HasValue);
        }

        public async Task<bool> ExistsByYearsOfServiceAsync(int yearsOfService, long? excludeId = null)
        {
            return await _context.SalaryCoefficients.AnyAsync(item =>
                item.IsDeleted != true &&
                item.YearsOfService == yearsOfService &&
                (!excludeId.HasValue || item.Id != excludeId.Value));
        }
    }
}
