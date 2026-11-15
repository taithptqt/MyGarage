using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Payroll.DTOs;

namespace MyGarage.Application.Payroll;

public interface IPayrollApplicationService
{
    Task<SalaryProfilePageResultDto> GetSalaryProfilesPageAsync(SalaryProfilePageQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<SalaryProfileDto?> GetSalaryProfileByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<SalaryProfileDto> CreateSalaryProfileAsync(SalaryProfileUpsertCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<SalaryProfileDto> UpdateSalaryProfileAsync(long id, SalaryProfileUpsertCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<bool> DeleteSalaryProfileAsync(long id, UserActorContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<SalaryRequestDto>> GetSalaryRequestsPageAsync(SalaryRequestPageQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<bool> ApproveSalaryRequestAsync(long requestId, SalaryApprovalDecisionCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<bool> RejectSalaryRequestAsync(long requestId, SalaryApprovalDecisionCommand command, UserActorContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<PayrollRunDto>> GetPayrollRunsPageAsync(PayrollRunPageQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<PayrollRunDto?> GetPayrollRunByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<PayrollRunPreviewResultDto> PreviewPayrollRunAsync(PayrollRunPreviewQuery query, UserActorContext actor, CancellationToken ct = default);
    Task<PayrollRunDto> CreatePayrollRunAsync(PayrollRunCreateCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<PayrollRunDto> ApprovePayrollRunAsync(long id, UserActorContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<PayslipDto>> GetMyPayslipsAsync(PayslipsQuery query, UserActorContext actor, CancellationToken ct = default);
}
