using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses;

public sealed record PayrollRunEndpointDetailDto(
    long Id,
    long StaffId,
    string? StaffName,
    decimal BaseSalary,
    decimal AllowanceAmount,
    decimal CommissionAmount,
    decimal OvertimeAmount,
    decimal Bonus,
    decimal Deductions,
    decimal Penalty,
    decimal NetSalary,
    decimal NetPay,
    string? Status);

public sealed record PayrollRunEndpointPayloadDto(
    long Id,
    long PayrollPeriodId,
    string? Period,
    int RunNo,
    long? BranchId,
    string? Status,
    DateTime? CalculatedAt,
    DateTime? ApprovedAt,
    DateTime? PaidAt,
    DateTime CreatedAt,
    decimal TotalAmount,
    IReadOnlyList<PayrollRunEndpointDetailDto> Details);

public sealed record PayrollRunPreviewEndpointDetailDto(
    long StaffId,
    string StaffName,
    decimal BaseSalary,
    decimal AllowanceAmount,
    decimal CommissionAmount,
    decimal OvertimeAmount,
    decimal Bonus,
    decimal Deductions,
    decimal Penalty,
    decimal NetSalary);

public sealed record PayrollRunPreviewEndpointDto(
    string Period,
    long BranchId,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<PayrollRunPreviewEndpointDetailDto> Details);

public enum PayrollRunEndpointActionStatus
{
    Ok = 0,
    Created = 1,
    BadRequest = 2,
    NotFound = 3,
    Forbidden = 4
}

public sealed record PayrollRunEndpointActionResult<T>(
    PayrollRunEndpointActionStatus Status,
    T? Data = default,
    string? ErrorMessage = null)
{
    public static PayrollRunEndpointActionResult<T> Ok(T data)
        => new(PayrollRunEndpointActionStatus.Ok, data, null);

    public static PayrollRunEndpointActionResult<T> Created(T data)
        => new(PayrollRunEndpointActionStatus.Created, data, null);

    public static PayrollRunEndpointActionResult<T> BadRequest(string errorMessage)
        => new(PayrollRunEndpointActionStatus.BadRequest, default, errorMessage);

    public static PayrollRunEndpointActionResult<T> NotFound(string errorMessage)
        => new(PayrollRunEndpointActionStatus.NotFound, default, errorMessage);

    public static PayrollRunEndpointActionResult<T> Forbidden()
        => new(PayrollRunEndpointActionStatus.Forbidden, default, null);
}
