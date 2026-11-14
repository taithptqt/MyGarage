using System.Threading;
using System.Threading.Tasks;
using MyGarage.Services;

namespace MyGarage.Interfaces
{
    public interface IPayrollBatchCalculationService
    {
        Task<PayrollBatchCalculationResult> CalculatePayrollForAllAsync(
            PayrollBatchCalculationCommand command,
            CancellationToken cancellationToken = default);
    }
}
