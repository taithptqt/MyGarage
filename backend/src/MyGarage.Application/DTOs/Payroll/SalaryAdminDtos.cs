namespace MyGarage.Application.Payroll;

public sealed record SalaryAdminActorContext(
    long? UserId,
    string? Role,
    string? RoleCode,
    string? RoleName);

public sealed record SalaryAdminCommandResult(
    int StatusCode,
    object? Payload = null,
    string? ProblemMessage = null)
{
    public static SalaryAdminCommandResult Ok(object? payload) => new(200, payload);
    public static SalaryAdminCommandResult Forbid() => new(403);
    public static SalaryAdminCommandResult NotFound(string? problemMessage = null) => new(404, null, problemMessage);
    public static SalaryAdminCommandResult BadRequest(string message) => new(400, null, message);
    public static SalaryAdminCommandResult Conflict(string message) => new(409, null, message);
}

public sealed class SalaryProfileUpsertDto
{
    public long StaffId { get; set; }
    public long? BranchId { get; set; }
    public decimal? BaseSalary { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
}

public sealed class SalaryApprovalDecisionDto
{
    public long? StaffId { get; set; }
    public decimal? BaseSalary { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public string? Note { get; set; }
    public string? RejectReason { get; set; }
}
