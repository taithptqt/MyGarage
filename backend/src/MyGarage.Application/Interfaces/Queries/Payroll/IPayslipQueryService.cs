using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IPayslipQueryService
{
    Task<IReadOnlyList<Payslip>> GetMyPayslipsAsync(long staffId, string? period = null, CancellationToken ct = default);
}
