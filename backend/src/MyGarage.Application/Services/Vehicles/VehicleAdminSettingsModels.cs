using MyGarage.Enums;

namespace MyGarage.Services;

public sealed class VehicleAdminSettings
{
    public Dictionary<long, VehicleMetadata> VehicleMetadata { get; set; } = new();
    public List<VehicleStatusHistoryEntry> StatusHistory { get; set; } = [];
    public List<VehicleAssignmentHistoryEntry> AssignmentHistory { get; set; } = [];
    public List<VehicleAuditLogEntry> VehicleAuditLogs { get; set; } = [];
    public VehicleAlertRules AlertRules { get; set; } = new();
    public DateTime? UpdatedAt { get; set; }

    public static VehicleAdminSettings CreateDefault()
    {
        return new VehicleAdminSettings
        {
            AlertRules = new VehicleAlertRules
            {
                InShopTooLongHours = 72,
                StatusTooLongHours = 48,
                WaitingPartsTooLongHours = 48,
                CompletedNotDeliveredHours = 24,
                BayOccupiedTooLongHours = 48,
                MaxVehiclesPerStaff = 8
            },
            UpdatedAt = VietnamTime.Now
        };
    }

    public void Normalize()
    {
        VehicleMetadata ??= new Dictionary<long, VehicleMetadata>();
        StatusHistory ??= new List<VehicleStatusHistoryEntry>();
        AssignmentHistory ??= new List<VehicleAssignmentHistoryEntry>();
        VehicleAuditLogs ??= new List<VehicleAuditLogEntry>();
        AlertRules ??= new VehicleAlertRules();

        var normalizedMetadata = new Dictionary<long, VehicleMetadata>();
        foreach (var (key, value) in VehicleMetadata)
        {
            if (key <= 0) continue;
            var item = value ?? new VehicleMetadata();
            item.Normalize();
            normalizedMetadata[key] = item;
        }
        VehicleMetadata = normalizedMetadata;

        StatusHistory = StatusHistory
            .Where(x => x != null && x.VehicleId > 0 && x.ChangedAt != default)
            .OrderBy(x => x.ChangedAt)
            .Select(x =>
            {
                x.OldStatus = VehicleAdminSupport.NormalizeVehicleStatus(x.OldStatus);
                x.NewStatus = VehicleAdminSupport.NormalizeVehicleStatus(x.NewStatus);
                x.Note = VehicleAdminSupport.NormalizeNullableText(x.Note);
                x.ChangedByName = VehicleAdminSupport.NormalizeNullableText(x.ChangedByName);
                return x;
            })
            .ToList();

        AssignmentHistory = AssignmentHistory
            .Where(x => x != null && x.VehicleId > 0 && x.AssignedAt != default)
            .OrderBy(x => x.AssignedAt)
            .Select(x =>
            {
                x.Note = VehicleAdminSupport.NormalizeNullableText(x.Note);
                x.TechnicalTeam = VehicleAdminSupport.NormalizeNullableText(x.TechnicalTeam);
                x.AssignedByName = VehicleAdminSupport.NormalizeNullableText(x.AssignedByName);
                x.OldStaffName = VehicleAdminSupport.NormalizeNullableText(x.OldStaffName);
                x.NewStaffName = VehicleAdminSupport.NormalizeNullableText(x.NewStaffName);
                return x;
            })
            .ToList();

        VehicleAuditLogs = VehicleAuditLogs
            .Where(x => x != null && x.VehicleId > 0 && x.CreatedAt != default)
            .OrderBy(x => x.CreatedAt)
            .Select(x =>
            {
                x.Action = VehicleAdminSupport.NormalizeKey(x.Action);
                x.Detail = VehicleAdminSupport.NormalizeNullableText(x.Detail);
                x.ActorName = VehicleAdminSupport.NormalizeNullableText(x.ActorName);
                return x;
            })
            .ToList();

        AlertRules.Normalize();
    }
}

public sealed class VehicleMetadata
{
    public bool IsEditLocked { get; set; }
    public bool IsHidden { get; set; }
    public string CurrentStatus { get; set; } = VehicleAdminSupport.StatusWaitingReceive;
    public DateTime? StatusUpdatedAt { get; set; }
    public long? AssignedBranchId { get; set; }
    public long? CurrentStaffId { get; set; }
    public string? TechnicalTeam { get; set; }
    public string? Version { get; set; }
    public string? EngineNumber { get; set; }
    public string? ChassisNumber { get; set; }
    public string? InitialCondition { get; set; }
    public string? ExpectedArea { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public void Normalize()
    {
        CurrentStatus = VehicleAdminSupport.NormalizeVehicleStatus(CurrentStatus);
        TechnicalTeam = VehicleAdminSupport.NormalizeNullableText(TechnicalTeam);
        Version = VehicleAdminSupport.NormalizeNullableText(Version);
        EngineNumber = VehicleAdminSupport.NormalizeNullableText(EngineNumber);
        ChassisNumber = VehicleAdminSupport.NormalizeNullableText(ChassisNumber);
        InitialCondition = VehicleAdminSupport.NormalizeNullableText(InitialCondition);
        ExpectedArea = VehicleAdminSupport.NormalizeNullableText(ExpectedArea);
    }
}

public sealed class VehicleStatusHistoryEntry
{
    public long VehicleId { get; set; }
    public string OldStatus { get; set; } = VehicleAdminSupport.StatusWaitingReceive;
    public string NewStatus { get; set; } = VehicleAdminSupport.StatusWaitingReceive;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
    public long? ChangedBy { get; set; }
    public string? ChangedByName { get; set; }
}

public sealed class VehicleAssignmentHistoryEntry
{
    public long VehicleId { get; set; }
    public long? OldStaffId { get; set; }
    public string? OldStaffName { get; set; }
    public long? NewStaffId { get; set; }
    public string? NewStaffName { get; set; }
    public string? TechnicalTeam { get; set; }
    public string? Note { get; set; }
    public DateTime AssignedAt { get; set; }
    public long? AssignedBy { get; set; }
    public string? AssignedByName { get; set; }
}

public sealed class VehicleAuditLogEntry
{
    public long VehicleId { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? ActorId { get; set; }
    public string? ActorName { get; set; }
}

public sealed class VehicleAlertRules
{
    public int InShopTooLongHours { get; set; } = 72;
    public int StatusTooLongHours { get; set; } = 48;
    public int WaitingPartsTooLongHours { get; set; } = 48;
    public int CompletedNotDeliveredHours { get; set; } = 24;
    public int BayOccupiedTooLongHours { get; set; } = 48;
    public int MaxVehiclesPerStaff { get; set; } = 8;

    public void Normalize()
    {
        InShopTooLongHours = Math.Clamp(InShopTooLongHours, 1, 24 * 180);
        StatusTooLongHours = Math.Clamp(StatusTooLongHours, 1, 24 * 180);
        WaitingPartsTooLongHours = Math.Clamp(WaitingPartsTooLongHours, 1, 24 * 180);
        CompletedNotDeliveredHours = Math.Clamp(CompletedNotDeliveredHours, 1, 24 * 180);
        BayOccupiedTooLongHours = Math.Clamp(BayOccupiedTooLongHours, 1, 24 * 180);
        MaxVehiclesPerStaff = Math.Clamp(MaxVehiclesPerStaff, 1, 100);
    }
}

public sealed class BaySettingsSnapshot
{
    public Dictionary<long, BayMetadataSnapshot> BayMetadata { get; set; } = new();
    public List<BaySessionSnapshot> Sessions { get; set; } = [];

    public static BaySettingsSnapshot CreateDefault() => new();

    public void Normalize()
    {
        BayMetadata ??= new Dictionary<long, BayMetadataSnapshot>();
        Sessions ??= new List<BaySessionSnapshot>();

        var normalizedMetadata = new Dictionary<long, BayMetadataSnapshot>();
        foreach (var (key, value) in BayMetadata)
        {
            if (key <= 0) continue;
            var item = value ?? BayMetadataSnapshot.CreateDefault();
            item.Normalize();
            normalizedMetadata[key] = item;
        }
        BayMetadata = normalizedMetadata;

        Sessions = Sessions
            .Where(x => x != null && x.BayId > 0 && x.VehicleId > 0 && x.AssignedAt != default)
            .OrderBy(x => x.AssignedAt)
            .Select(x =>
            {
                if (string.IsNullOrWhiteSpace(x.SessionId)) x.SessionId = Guid.NewGuid().ToString("N");
                if (x.ReleasedAt.HasValue && x.ReleasedAt.Value < x.AssignedAt) x.ReleasedAt = x.AssignedAt;
                x.ServiceStatus = VehicleAdminSupport.NormalizeNullableText(x.ServiceStatus);
                x.Note = VehicleAdminSupport.NormalizeNullableText(x.Note);
                return x;
            })
            .ToList();
    }
}

public sealed class BayMetadataSnapshot
{
    public string Area { get; set; } = "Unassigned";
    public int Capacity { get; set; } = 1;
    public List<string> SupportedSegments { get; set; } = [];

    public static BayMetadataSnapshot CreateDefault() => new();

    public void Normalize()
    {
        Area = string.IsNullOrWhiteSpace(Area) ? "Unassigned" : Area.Trim();
        Capacity = Math.Clamp(Capacity, 1, 20);
        SupportedSegments = (SupportedSegments ?? [])
            .Select(VehicleAdminSupport.NormalizeSegment)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class BaySessionSnapshot
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public long BayId { get; set; }
    public long VehicleId { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? ServiceStatus { get; set; }
    public string? Note { get; set; }
}

internal sealed class VehicleViewRow
{
    public long Id { get; set; }
    public string VehicleCode { get; set; } = "";
    public string LicensePlate { get; set; } = "";
    public string? Vin { get; set; }
    public long ModelId { get; set; }
    public string ModelName { get; set; } = "";
    public long BrandId { get; set; }
    public string BrandName { get; set; } = "";
    public string? Segment { get; set; }
    public string? Color { get; set; }
    public int? ManufactureYear { get; set; }
    public int? Odo { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string CurrentStatus { get; set; } = VehicleAdminSupport.StatusWaitingReceive;
    public DateTime StatusSince { get; set; }
    public long? CurrentBayId { get; set; }
    public string? CurrentBayCode { get; set; }
    public string? CurrentBayName { get; set; }
    public string? CurrentBayArea { get; set; }
    public long? CurrentStaffId { get; set; }
    public string? CurrentStaffName { get; set; }
    public long? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? TechnicalTeam { get; set; }
    public string? ExpectedArea { get; set; }
    public string? Version { get; set; }
    public string? EngineNumber { get; set; }
    public string? ChassisNumber { get; set; }
    public string? InitialCondition { get; set; }
    public bool IsEditLocked { get; set; }
    public bool IsHidden { get; set; }
    public double InShopHours { get; set; }
    public double StatusDurationHours { get; set; }
    public double BayOccupiedHours { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class VehicleAlertItem
{
    public string Severity { get; set; } = "";
    public string Type { get; set; } = "";
    public long VehicleId { get; set; }
    public string VehicleCode { get; set; } = "";
    public string LicensePlate { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

internal sealed class VehicleTimelineItem
{
    public long VehicleId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? ActorId { get; set; }
    public string? ActorName { get; set; }
}

internal sealed class AssignBayResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";

    public static AssignBayResult Ok() => new() { Success = true, Message = "OK" };
    public static AssignBayResult Fail(string message) => new() { Success = false, Message = message };
}
