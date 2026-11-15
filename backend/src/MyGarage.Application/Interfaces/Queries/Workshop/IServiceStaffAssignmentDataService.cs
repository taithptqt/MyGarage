using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record ServiceOrderDetailSummary(
    string? ItemName,
    string? Description,
    string? PayerType);

public sealed record StaffTechnicianSummary(
    long Id,
    string? FullName,
    string? Specialty,
    long? BranchId);

public interface IServiceStaffAssignmentDataService :
    IServiceStaffAssignmentQueryDataService,
    IServiceStaffAssignmentRosterDataService,
    IServiceStaffAssignmentLifecycleDataService
{
}
