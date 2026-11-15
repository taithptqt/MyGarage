using MyGarage.Enums;

namespace MyGarage.Application.Bays;

internal static class BayAdminConstants
{
    public const string StatusEmpty = "EMPTY";
    public const string StatusActive = nameof(BayOperationalStatusCode.ACTIVE);
    public const string StatusUnderMaintenance = nameof(BayOperationalStatusCode.UNDER_MAINTENANCE);
    public const string StatusBroken = nameof(BayOperationalStatusCode.BROKEN);
    public const string StatusInUse = "IN_USE";
    public const string StatusMaintenance = "MAINTENANCE";
    public const string StatusSuspended = "SUSPENDED";
    public const string DbBayStatusActive = nameof(BayOperationalStatusCode.ACTIVE);
    public const string DbBayStatusBroken = nameof(BayOperationalStatusCode.BROKEN);
    public const string DbBayStatusUnderMaintenance = nameof(BayOperationalStatusCode.UNDER_MAINTENANCE);
    public const int ReservationStatusAvailable = 0;
    public const int ReservationStatusOccupied = 2;
}

internal sealed class BayAdminSettings
{
    public Dictionary<long, BayMetadata> BayMetadata { get; set; } = new();
    public List<BaySession> Sessions { get; set; } = [];
    public BayAlertRules AlertRules { get; set; } = new();
    public DateTime? UpdatedAt { get; set; }

    public static BayAdminSettings CreateDefault()
    {
        return new BayAdminSettings
        {
            AlertRules = new BayAlertRules
            {
                LongOccupancyHours = 24,
                InactiveDays = 30
            },
            UpdatedAt = VietnamTime.Now
        };
    }

    public void Normalize()
    {
        BayMetadata ??= new Dictionary<long, BayMetadata>();
        Sessions ??= new List<BaySession>();
        AlertRules ??= new BayAlertRules();

        var normalizedMetadata = new Dictionary<long, BayMetadata>();
        foreach (var (key, value) in BayMetadata)
        {
            if (key <= 0) continue;
            var meta = value ?? Bays.BayMetadata.CreateDefault();
            meta.Normalize();
            normalizedMetadata[key] = meta;
        }
        BayMetadata = normalizedMetadata;

        Sessions = Sessions
            .Where(s => s != null && s.BayId > 0 && s.VehicleId > 0 && s.AssignedAt != default)
            .OrderBy(s => s.AssignedAt)
            .Select(s =>
            {
                if (string.IsNullOrWhiteSpace(s.SessionId))
                {
                    s.SessionId = Guid.NewGuid().ToString("N");
                }
                if (s.ReleasedAt.HasValue && s.ReleasedAt.Value < s.AssignedAt)
                {
                    s.ReleasedAt = s.AssignedAt;
                }
                return s;
            })
            .ToList();

        AlertRules.Normalize();
    }
}

internal sealed class BayMetadata
{
    public string Area { get; set; } = "Chưa phân khu";
    public int Capacity { get; set; } = 1;
    public List<string> SupportedSegments { get; set; } = [];

    public static BayMetadata CreateDefault() => new();

    public void Normalize()
    {
        Area = BayAdminSettingsHelper.NormalizeArea(Area);
        Capacity = BayAdminSettingsHelper.NormalizeCapacity(Capacity);
        SupportedSegments = BayAdminSettingsHelper.NormalizeSegments(SupportedSegments);
    }
}

internal sealed class BaySession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public long BayId { get; set; }
    public long VehicleId { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? ServiceStatus { get; set; }
    public string? Note { get; set; }
}

internal sealed class BayAlertRules
{
    public int LongOccupancyHours { get; set; } = 24;
    public int InactiveDays { get; set; } = 30;

    public void Normalize()
    {
        LongOccupancyHours = Math.Clamp(LongOccupancyHours, 1, 24 * 30);
        InactiveDays = Math.Clamp(InactiveDays, 1, 365);
    }
}

internal sealed class BayAlertItem
{
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public long BayId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string Area { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

internal sealed class BayUsageMetrics
{
    public int SessionCount { get; set; }
    public double TotalUsageHours { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public double LongestActiveHours { get; set; }
    public int VehicleHistoryCount { get; set; }
    public HashSet<long> VehicleIds { get; } = [];
    public List<BaySession> ActiveSessions { get; } = [];
}

internal sealed class BayViewRow
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public int SequenceOrder { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string BayType { get; set; } = "";
    public string Area { get; set; } = "";
    public int Capacity { get; set; }
    public IReadOnlyList<string> SupportedSegments { get; set; } = [];
    public decimal? MaxTonnage { get; set; }
    public string OperationalStatus { get; set; } = BayAdminConstants.StatusActive;
    public bool IsLocked { get; set; }
    public bool IsHidden { get; set; }
    public int OccupiedCount { get; set; }
    public double UtilizationRate { get; set; }
    public int SessionCount { get; set; }
    public double TotalUsageHours { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public double LongestActiveHours { get; set; }
    public int VehicleHistoryCount { get; set; }
    public IReadOnlyList<BayVehicleView> ActiveVehicles { get; set; } = [];
}

internal sealed class BayVehicleView
{
    public long VehicleId { get; set; }
    public string LicensePlate { get; set; } = "";
    public string? ModelName { get; set; }
    public string? BrandName { get; set; }
    public string? Segment { get; set; }
    public DateTime? AssignedAt { get; set; }
    public int OccupiedMinutes { get; set; }
}
