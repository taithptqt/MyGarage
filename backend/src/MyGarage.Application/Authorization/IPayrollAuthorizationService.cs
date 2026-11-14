using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IPayrollAuthorizationService
{
    Task<bool> CanViewSalaryProfilesAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanManageSalaryProfilesAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewPayrollRunsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanCalculatePayrollAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanApprovePayrollAsync(long payrollRunId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanMarkPaidPayrollAsync(long payrollRunId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewMyPayslipAsync(long staffId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanAccessStaffSalaryAsync(long staffId, UserActorContext actor, CancellationToken ct = default);
}
