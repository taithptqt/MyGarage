using System.ComponentModel.DataAnnotations;
using MyGarage.Enums;

namespace MyGarage.Interfaces;

public sealed record VehicleAdminCommandResult(int StatusCode, object? Payload = null)
{
    public static VehicleAdminCommandResult Ok(object? payload = null) => new(200, payload);
    public static VehicleAdminCommandResult BadRequest(string message) => new(400, new { message });
    public static VehicleAdminCommandResult NotFound(string message) => new(404, new { message });
}

public sealed record VehicleAdminActorContext(
    long? UserId,
    string? UserDisplayName);

public sealed record VehicleAdminListQuery(
    long? BranchId,
    long? BrandId,
    long? ModelId,
    string? Color,
    int? ManufactureYear,
    string? Status,
    long? BayId,
    long? StaffId,
    DateTime? CheckInFrom,
    DateTime? CheckInTo,
    string? Search,
    string? SortBy = "checkin",
    string? SortDirection = "desc",
    bool IncludeHidden = false);

public sealed class VehicleUpsertRequest
{
    [RegularExpression(ValidationPatterns.VietnamLicensePlate, ErrorMessage = ValidationMessages.VietnamLicensePlate)]
    public string? LicensePlate { get; set; }
    [RegularExpression(ValidationPatterns.Vin17, ErrorMessage = ValidationMessages.Vin17)]
    public string? Vin { get; set; }
    public long ModelId { get; set; }
    public string? Color { get; set; }
    public int? Odo { get; set; }
    [Range(1900, 2100, ErrorMessage = "Năm sản xuất không hợp lệ.")]
    public int? ManufactureYear { get; set; }
    public long? CustomerId { get; set; }
    public long? BranchId { get; set; }
    public string? Status { get; set; }
    public string? Version { get; set; }
    public string? EngineNumber { get; set; }
    public string? ChassisNumber { get; set; }
    public string? InitialCondition { get; set; }
    public string? TechnicalTeam { get; set; }
    public string? ExpectedArea { get; set; }
    public string? Note { get; set; }
}

public sealed class ChangeStatusRequest
{
    public string? Status { get; set; }
    public string? Note { get; set; }
}

public sealed class ChangeOwnerRequest
{
    public long? CustomerId { get; set; }
    public string? Note { get; set; }
}

public sealed class AssignBayRequest
{
    public long BayId { get; set; }
    public string? ServiceStatus { get; set; }
    public string? Note { get; set; }
    public string? NewVehicleStatus { get; set; }
}

public sealed class MoveBayRequest
{
    public long ToBayId { get; set; }
    public string? ServiceStatus { get; set; }
    public string? Note { get; set; }
    public string? NewVehicleStatus { get; set; }
}

public sealed class RemoveBayRequest
{
    public string? Note { get; set; }
}

public sealed class AssignStaffRequest
{
    public long? StaffId { get; set; }
    public string? TechnicalTeam { get; set; }
    public string? Note { get; set; }
}

public interface IVehicleAdminCommandFacade :
    IVehicleAdminQueryService,
    IVehicleAdminLifecycleService,
    IVehicleAdminAssignmentService
{
}
