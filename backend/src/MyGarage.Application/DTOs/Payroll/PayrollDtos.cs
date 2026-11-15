using System;
using System.Collections.Generic;

namespace MyGarage.Application.Payroll.DTOs;

public sealed record SalaryProfileDto(
    long Id,
    long StaffId,
    string? StaffName,
    long? BranchId,
    string? BranchName,
    decimal BaseSalary,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    string Status,
    string? Note,
    long? UpdatedBy,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);

public sealed record SalaryProfileUpsertCommand(
    long StaffId,
    long? BranchId,
    decimal? BaseSalary,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    string? Status,
    string? Note
);

public sealed record SalaryProfilePageQuery(
    long? BranchId = null,
    long? StaffId = null,
    string? Status = null,
    int? Page = null,
    int? PageSize = null
);

public sealed record SalaryProfilePageResultDto(
    IReadOnlyList<SalaryProfileDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record SalaryRequestDto(
    long Id,
    long StaffId,
    string? StaffName,
    long? BranchId,
    decimal? ProposedBaseSalary,
    DateTime? ProposedEffectiveFrom,
    string Status,
    string? Note,
    string? RejectReason,
    DateTime? CreatedAt
);

public sealed record SalaryRequestPageQuery(
    long? BranchId = null,
    string? Status = null,
    int? Page = null,
    int? PageSize = null
);

public sealed record SalaryApprovalDecisionCommand(
    long? StaffId,
    decimal? BaseSalary,
    DateTime? EffectiveFrom,
    string? Note,
    string? RejectReason
);

public sealed record PayrollRunDto(
    long Id,
    long PayrollPeriodId,
    string? PeriodCode,
    long? BranchId,
    string? BranchName,
    int RunNo,
    string Status,
    decimal TotalAmount,
    int EmployeeCount,
    long? CalculatedBy,
    DateTime? CalculatedAt,
    long? ApprovedBy,
    DateTime? ApprovedAt,
    DateTime? PaidAt,
    IReadOnlyList<PayrollRunItemDto>? Details
);

public sealed record PayrollRunItemDto(
    long Id,
    long StaffId,
    string? StaffName,
    decimal BaseSalary,
    decimal AllowanceAmount,
    decimal CommissionAmount,
    decimal OvertimeAmount,
    decimal BonusAmount,
    decimal AttendanceDeduction,
    decimal AdvanceDeduction,
    decimal LoanDeduction,
    decimal InsuranceEmployeeAmount,
    decimal PitAmount,
    decimal OtherDeduction,
    decimal PenaltyAmount,
    decimal NetPay,
    string Status
);

public sealed record PayrollRunPageQuery(
    long? BranchId = null,
    string? Period = null,
    string? Status = null,
    int? Page = null,
    int? PageSize = null
);

public sealed record PayrollRunPreviewQuery(
    long? BranchId,
    string? Period
);

public sealed record PayrollRunPreviewItemDto(
    long StaffId,
    string? StaffName,
    decimal BaseSalary,
    decimal AllowanceAmount,
    decimal CommissionAmount,
    decimal OvertimeAmount,
    decimal Bonus,
    decimal Deductions,
    decimal Penalty,
    decimal NetSalary
);

public sealed record PayrollRunPreviewResultDto(
    string Period,
    long? BranchId,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<PayrollRunPreviewItemDto> Details
);

public sealed record PayrollRunCreateCommand(
    string? Period,
    long? BranchId,
    long? CreatedBy,
    IReadOnlyList<PayrollRunCreateItemCommand>? Details
);

public sealed record PayrollRunCreateItemCommand(
    long StaffId,
    decimal? BaseSalary,
    decimal? CommissionAmount,
    decimal? OvertimeAmount,
    decimal? AllowanceAmount,
    decimal? Bonus,
    decimal? BonusAmount,
    decimal? Deductions,
    decimal? Penalty,
    decimal? PenaltyAmount
);

public sealed record PayslipsQuery(
    long? StaffId = null,
    string? Period = null,
    int? Page = null,
    int? PageSize = null
);

public sealed record PayslipDto(
    long Id,
    long PayrollItemId,
    string? SlipNo,
    long StaffId,
    string? StaffName,
    string? PeriodCode,
    decimal BaseSalary,
    decimal AllowanceAmount,
    decimal CommissionAmount,
    decimal OvertimeAmount,
    decimal BonusAmount,
    decimal TotalDeductions,
    decimal NetPay,
    DateTime? IssuedAt,
    DateTime? ViewedAt
);
