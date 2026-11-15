namespace MyGarage.Application.Bays;

public sealed record BayAdminCommandResult(int StatusCode, object? Payload = null)
{
    public static BayAdminCommandResult Ok(object? payload = null) => new(200, payload);
    public static BayAdminCommandResult BadRequest(string message) => new(400, new { message });
    public static BayAdminCommandResult NotFound(string message) => new(404, new { message });
}

public sealed class BayUpsertRequest
{
    public long BranchId { get; set; }
    public int? SequenceOrder { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? BayType { get; set; }
    public decimal? MaxTonnage { get; set; }
    public string? OperationalStatus { get; set; }
    public bool? IsActive { get; set; }
    public string? Area { get; set; }
    public int? Capacity { get; set; }
    public List<string>? SupportedSegments { get; set; }
}

public sealed class UpdateBayStatusRequest
{
    public string? Status { get; set; }
}

public sealed class AssignVehicleRequest
{
    public long VehicleId { get; set; }
    public string? ServiceStatus { get; set; }
    public string? Note { get; set; }
}

public sealed class RemoveVehicleRequest
{
    public long? VehicleId { get; set; }
    public string? Note { get; set; }
}

public sealed class MoveVehicleRequest
{
    public long VehicleId { get; set; }
    public long FromBayId { get; set; }
    public long ToBayId { get; set; }
    public string? ServiceStatus { get; set; }
    public string? Note { get; set; }
}

public interface IBayAdminCommandFacade
    : IBayAdminQueryService,
      IBayAdminManagementService,
      IBayAdminVehicleAssignmentService
{
}
