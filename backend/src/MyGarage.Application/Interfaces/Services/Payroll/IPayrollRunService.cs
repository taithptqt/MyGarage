using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Payroll.DTOs;

namespace MyGarage.Application.Payroll;

public interface IPayrollRunService
{
    Task<IReadOnlyList<PayrollRunDto>> GetPayrollRunsPageAsync(PayrollRunPageQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<PayrollRunDto?> GetPayrollRunByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<PayrollRunPreviewResultDto> PreviewPayrollRunAsync(PayrollRunPreviewQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<PayrollRunDto> CreatePayrollRunAsync(PayrollRunCreateCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<PayrollRunDto> ApprovePayrollRunAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<PayslipDto>> GetMyPayslipsAsync(PayslipsQuery query, UserActorContext actor, CancellationToken ct = default);
}
