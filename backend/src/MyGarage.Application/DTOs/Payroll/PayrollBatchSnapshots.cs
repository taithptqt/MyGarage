using System;

namespace MyGarage.Application.Payroll.DTOs;

public sealed record SalaryCoefficientThreshold(int YearsOfService, decimal CoefficientValue);

public sealed class StaffPayrollSnapshot
{
    public long StaffId { get; init; }
    public long? BranchId { get; init; }
    public string StaffName { get; init; } = "";
    public DateOnly? HireDate { get; init; }
    public DateOnly? TerminationDate { get; init; }
    public string? Status { get; init; }
}
