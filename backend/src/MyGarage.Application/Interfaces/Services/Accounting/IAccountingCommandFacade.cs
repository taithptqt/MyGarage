using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Accounting;

public sealed record AccountingCommandResult(
    int StatusCode,
    object? Payload = null,
    string? ProblemMessage = null)
{
    public static AccountingCommandResult Problem(int statusCode, string message) =>
        new(statusCode, null, message);
}

public sealed record AccountingFileResult(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record IssuePayslipCommand(
    long PayrollItemId,
    string? SlipNo,
    string? IssuedAt,
    long? IssuedBy,
    string? FileUrl);

public sealed record CreatePayrollPeriodCommand(
    string? PeriodCode,
    string? StartDate,
    string? EndDate,
    string? PayrollDate,
    string? Status);

public sealed record InitialSalaryCommand(
    decimal? BaseSalary,
    DateTime? EffectiveFrom,
    string? Reason,
    string? Note);

public interface IAccountingCommandFacade :
    IAccountingTableAndInvoiceService,
    IAccountingFinanceWorkflowService,
    IAccountingPayrollCommandService
{
}
