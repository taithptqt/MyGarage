namespace MyGarage.DTOs.Responses;

public sealed class CustomerPortalDashboardDto
{
    public List<CustomerPortalActiveTrackDto> ActiveTracks { get; set; } = new();
    public List<CustomerPortalUpcomingAppointmentDto> UpcomingAppointments { get; set; } = new();
    public CustomerPortalLastServiceDto? LastCompletedService { get; set; }
}

public sealed class CustomerPortalActiveTrackDto
{
    public long VehicleId { get; set; }
    public string LicensePlate { get; set; } = "";
    public string? ModelLabel { get; set; }
    public long ServiceOrderId { get; set; }
    public string OrderCode { get; set; } = "";
    public string LifecycleStatus { get; set; } = "";
    public string CustomerStageCode { get; set; } = "";
    public string CustomerStageLabel { get; set; } = "";
    public List<CustomerPortalTimelineStepDto> Timeline { get; set; } = new();
    public long BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime? EstimatedHandover { get; set; }
    public DateTime? PromisedHandoverTime { get; set; }
    public string? ServiceAdvisorName { get; set; }
    /// <summary>Technicians actually assigned via ServiceStaffAssignments (may be empty when no assignments yet).</summary>
    public List<string> AssignedTechnicianNames { get; set; } = new();
    public string? BayCode { get; set; }
    public string? BayName { get; set; }
    public string? BayType { get; set; }
    public string? ServiceArea { get; set; }
    public DateTime? LocationReportedAt { get; set; }
    public string? LocationReportedBy { get; set; }
    public string? LocationNote { get; set; }
}

public sealed class CustomerPortalTimelineStepDto
{
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public string State { get; set; } = "";
}

public sealed class CustomerPortalUpcomingAppointmentDto
{
    public long Id { get; set; }
    public long? VehicleId { get; set; }
    public string? VehiclePlate { get; set; }
    public long BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = "";
}

public sealed class CustomerPortalLastServiceDto
{
    public long ServiceOrderId { get; set; }
    public string OrderCode { get; set; } = "";
    public long? VehicleId { get; set; }
    public string? LicensePlate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? BranchName { get; set; }
}

public sealed class CustomerPortalVehicleDto
{
    public long VehicleId { get; set; }
    public long CustomerVehicleId { get; set; }
    public string LicensePlate { get; set; } = "";
    public string? Vin { get; set; }
    public long ModelId { get; set; }
    public string? BrandName { get; set; }
    public string? ModelName { get; set; }
    public string? Color { get; set; }
    public int? ManufactureYear { get; set; }
    public int? Odo { get; set; }
    public bool IsCurrentOwner { get; set; }
}

public sealed class CustomerPortalVehicleHistoryDto
{
    public List<CustomerPortalHistoryItemDto> Items { get; set; } = new();
    public List<CustomerPortalMaintenanceHintDto> MaintenanceHints { get; set; } = new();
}

public sealed class CustomerPortalHistoryItemDto
{
    public long ServiceOrderId { get; set; }
    public string OrderCode { get; set; } = "";
    public long BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "";
    public string CustomerStageLabel { get; set; } = "";
    public List<string> LineSummaries { get; set; } = new();
    public decimal? TotalAmount { get; set; }
    public string? PaymentStatus { get; set; }
    public long? InvoiceId { get; set; }
    public string? InvoiceUrl { get; set; }
    /// <summary>Condition log entries recorded at check-in / check-out (ODO, fuel, defects etc.).</summary>
    public List<CustomerPortalConditionLogDto> ConditionLogs { get; set; } = new();
}

/// <summary>A snapshot of vehicle condition captured during check-in or check-out.</summary>
public sealed class CustomerPortalConditionLogDto
{
    public long Id { get; set; }
    public string LogType { get; set; } = "";
    public int? OdoReading { get; set; }
    public byte? FuelLevel { get; set; }
    public string? DashboardWarningLights { get; set; }
    public string? Valuables { get; set; }
    public string? DefectsAndImages { get; set; }
    public string? CustomerSignatureUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class CustomerPortalMaintenanceHintDto
{
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime? PredictedDate { get; set; }
}

public sealed class CustomerPortalNotificationDto
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LinkAction { get; set; }
    public string DerivedType { get; set; } = "";
}

public sealed class CustomerPortalServiceOrderLinkContextDto
{
    public long ServiceOrderId { get; set; }
    public long VehicleId { get; set; }
    public string TargetPath { get; set; } = "";
    public string SuggestedTab { get; set; } = "history";
}

public sealed class CustomerPortalVehicleLookupDto
{
    public List<CustomerPortalBrandLookupDto> Brands { get; set; } = new();
    public List<CustomerPortalModelLookupDto> Models { get; set; } = new();
}

public sealed class CustomerPortalBrandLookupDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class CustomerPortalModelLookupDto
{
    public long Id { get; set; }
    public long BrandId { get; set; }
    public string BrandName { get; set; } = "";
    public string ModelName { get; set; } = "";
}
