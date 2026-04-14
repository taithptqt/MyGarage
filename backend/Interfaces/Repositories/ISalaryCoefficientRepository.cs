using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ISalaryCoefficientRepository : IGenericRepository<SalaryCoefficient>
    {
        Task<IEnumerable<SalaryCoefficient>> GetAllConfiguredAsync();
        Task<SalaryCoefficient?> GetConfiguredByIdAsync(long id);
        Task<bool> ExistsByYearsOfServiceAsync(int yearsOfService, long? excludeId = null);
    }
}
