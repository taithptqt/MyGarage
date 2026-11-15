using System.Text.Json;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class VehicleAdminSupport
{
    internal const string StatusWaitingReceive = "WAITING_RECEIVE";
    internal const string StatusReceived = "RECEIVED";
    internal const string StatusDiagnosing = nameof(ServiceOrderLifecycleStatus.DIAGNOSING);
    internal const string StatusRepairing = "REPAIRING";
    internal const string StatusInProgress = nameof(ServiceProgressStatus.IN_PROGRESS);
    internal const string StatusWaitingParts = nameof(ServiceOrderDetailStatusCode.WAITING_PARTS);
    internal const string StatusWaitingDelivery = "WAITING_DELIVERY";
    internal const string StatusCompleted = nameof(ServiceOrderDetailStatusCode.COMPLETED);
    internal const string StatusDelivered = nameof(RetailOrderStatus.DELIVERED);
    internal const string StatusCanceled = nameof(ServiceOrderDetailStatusCode.CANCELED);

    internal const string BayStatusEmpty = "EMPTY";
    internal const string BayStatusInUse = "IN_USE";
    internal const string BayStatusMaintenance = "MAINTENANCE";
    internal const string BayStatusSuspended = "SUSPENDED";
    private const string DbBayStatusActive = nameof(BayOperationalStatusCode.ACTIVE);
    private const string DbBayStatusBroken = nameof(BayOperationalStatusCode.BROKEN);
    private const string DbBayStatusUnderMaintenance = nameof(BayOperationalStatusCode.UNDER_MAINTENANCE);

    internal static readonly string[] VehicleStatusOptions =
    [
        StatusWaitingReceive,
        StatusReceived,
        StatusDiagnosing,
        StatusRepairing,
        StatusWaitingParts,
        StatusWaitingDelivery,
        StatusCompleted,
        StatusDelivered,
        StatusCanceled
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    internal static async Task<VehicleAdminSettings> ReadSettingsAsync(string contentRootPath)
    {
        var path = GetSettingsPath(contentRootPath, "vehicle-admin-settings.json");
        if (!File.Exists(path)) return VehicleAdminSettings.CreateDefault();

        try
        {
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<VehicleAdminSettings>(stream, JsonOptions)
                ?? VehicleAdminSettings.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return VehicleAdminSettings.CreateDefault();
        }
    }

    internal static async Task WriteSettingsAsync(string contentRootPath, VehicleAdminSettings settings)
    {
        settings.Normalize();
        settings.UpdatedAt = VietnamTime.Now;

        var path = GetSettingsPath(contentRootPath, "vehicle-admin-settings.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    internal static async Task<BaySettingsSnapshot> ReadBaySettingsAsync(string contentRootPath)
    {
        var path = GetSettingsPath(contentRootPath, "bay-admin-settings.json");
        if (!File.Exists(path)) return BaySettingsSnapshot.CreateDefault();

        try
        {
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<BaySettingsSnapshot>(stream, JsonOptions)
                ?? BaySettingsSnapshot.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return BaySettingsSnapshot.CreateDefault();
        }
    }

    internal static async Task WriteBaySettingsAsync(string contentRootPath, BaySettingsSnapshot settings)
    {
        settings.Normalize();
        var path = GetSettingsPath(contentRootPath, "bay-admin-settings.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    private static string GetSettingsPath(string contentRootPath, string fileName)
    {
        var folder = Path.Combine(contentRootPath, "Configurations");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, fileName);
    }

    internal static VehicleMetadata ResolveVehicleMetadata(VehicleAdminSettings settings, long vehicleId)
    {
        if (!settings.VehicleMetadata.TryGetValue(vehicleId, out var metadata) || metadata == null)
        {
            metadata = new VehicleMetadata();
            settings.VehicleMetadata[vehicleId] = metadata;
        }

        metadata.Normalize();
        return metadata;
    }

    internal static BayMetadataSnapshot ResolveBayMetadata(BaySettingsSnapshot settings, long bayId)
    {
        if (!settings.BayMetadata.TryGetValue(bayId, out var metadata) || metadata == null)
        {
            metadata = BayMetadataSnapshot.CreateDefault();
            settings.BayMetadata[bayId] = metadata;
        }

        metadata.Normalize();
        return metadata;
    }

    internal static void ApplyVehicleMetadata(VehicleMetadata metadata, VehicleUpsertRequest request, DateTime now)
    {
        if (request.Version != null) metadata.Version = NormalizeNullableText(request.Version);
        if (request.EngineNumber != null) metadata.EngineNumber = NormalizeNullableText(request.EngineNumber);
        if (request.ChassisNumber != null) metadata.ChassisNumber = NormalizeNullableText(request.ChassisNumber);
        if (request.InitialCondition != null) metadata.InitialCondition = NormalizeNullableText(request.InitialCondition);
        if (request.TechnicalTeam != null) metadata.TechnicalTeam = NormalizeNullableText(request.TechnicalTeam);
        if (request.ExpectedArea != null) metadata.ExpectedArea = NormalizeNullableText(request.ExpectedArea);
        metadata.UpdatedAt = now;
    }

    internal static void UpdateVehicleStatus(
        VehicleMetadata metadata,
        VehicleAdminSettings settings,
        long vehicleId,
        string newStatus,
        string? note,
        long? userId,
        DateTime changedAt,
        string? actorName)
    {
        var oldStatus = NormalizeVehicleStatus(metadata.CurrentStatus);
        if (string.IsNullOrWhiteSpace(oldStatus)) oldStatus = StatusWaitingReceive;
        if (oldStatus == newStatus && metadata.StatusUpdatedAt.HasValue) return;

        metadata.CurrentStatus = newStatus;
        metadata.StatusUpdatedAt = changedAt;
        metadata.UpdatedAt = changedAt;

        settings.StatusHistory.Add(new VehicleStatusHistoryEntry
        {
            VehicleId = vehicleId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Note = NormalizeNullableText(note),
            ChangedAt = changedAt,
            ChangedBy = userId,
            ChangedByName = actorName
        });
    }

    internal static async Task SetCurrentOwnerAsync(
        IVehicleAdminDataService dataService,
        long vehicleId,
        long? customerId,
        string? note,
        DateTime now,
        long? userId,
        CancellationToken ct)
    {
        var links = await dataService.GetActiveCustomerVehicleLinksAsync(vehicleId, ct);

        var today = DateOnly.FromDateTime(now);
        foreach (var link in links.Where(x => x.IsCurrentOwner == true))
        {
            link.IsCurrentOwner = false;
            link.TransferDate = today;
            link.UpdatedAt = now;
            link.UpdatedBy = userId;
        }

        if (!customerId.HasValue) return;

        var customerExists = await dataService.CustomerExistsAsync(customerId.Value, ct);
        if (!customerExists) throw new InvalidOperationException("Customer does not exist.");

        var target = links.FirstOrDefault(x => x.CustomerId == customerId.Value);
        if (target == null)
        {
            target = new CustomerVehicle
            {
                CustomerId = customerId.Value,
                VehicleId = vehicleId,
                IsCurrentOwner = true,
                OwnershipStartDate = today,
                TransferDate = null,
                Note = NormalizeNullableText(note),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
                IsDeleted = false
            };
            dataService.AddCustomerVehicle(target);
        }
        else
        {
            target.IsCurrentOwner = true;
            target.TransferDate = null;
            target.Note = MergeNote(target.Note, note);
            target.UpdatedAt = now;
            target.UpdatedBy = userId;
            if (!target.OwnershipStartDate.HasValue) target.OwnershipStartDate = today;
        }
    }

    internal static string FormatVehicleCode(long vehicleId) => $"VEH-{vehicleId:000000}";

    internal static void AppendVehicleAudit(
        VehicleAdminSettings settings,
        long vehicleId,
        string action,
        string? detail,
        long? actorId,
        string? actorName,
        DateTime createdAt)
    {
        settings.VehicleAuditLogs.Add(new VehicleAuditLogEntry
        {
            VehicleId = vehicleId,
            Action = NormalizeKey(action),
            Detail = NormalizeNullableText(detail),
            ActorId = actorId,
            ActorName = actorName,
            CreatedAt = createdAt
        });
    }

    internal static bool IsKnownVehicleStatus(string status) => VehicleStatusOptions.Contains(status, StringComparer.OrdinalIgnoreCase);
    internal static bool IsBusyStatus(string status) => status != StatusDelivered && status != StatusCanceled;
    internal static bool IsInShopStatus(string status) => status != StatusDelivered && status != StatusCanceled;

    internal static string NormalizeVehicleStatus(string? status)
    {
        var normalized = NormalizeKey(status);
        return normalized switch
        {
            "" => StatusWaitingReceive,
            nameof(ServiceOrderLifecycleStatus.NEW) or nameof(DiagnosisStatus.PENDING) or "WAITING" or "CHO_TIEP_NHAN" or "WAITING_RECEIVE" => StatusWaitingReceive,
            "RECEIVED" or "DA_TIEP_NHAN" => StatusReceived,
            nameof(ServiceOrderLifecycleStatus.DIAGNOSING) or "CHECKING" or "DANG_KIEM_TRA" => StatusDiagnosing,
            "REPAIRING" or StatusInProgress or "DANG_SUA" => StatusRepairing,
            nameof(ServiceOrderDetailStatusCode.WAITING_PARTS) or "CHO_PHU_TUNG" => StatusWaitingParts,
            "WAITING_DELIVERY" or "CHO_GIAO" => StatusWaitingDelivery,
            nameof(ServiceOrderDetailStatusCode.COMPLETED) or "DONE" or "HOAN_TAT" => StatusCompleted,
            nameof(RetailOrderStatus.DELIVERED) or "DA_GIAO" or "HANDOVER" => StatusDelivered,
            nameof(AppointmentStatus.CANCELED) or nameof(ServiceOrderDetailStatusCode.CANCELED) or "HUY" => StatusCanceled,
            _ => normalized
        };
    }

    internal static string NormalizeBayStatus(string? status)
    {
        var normalized = NormalizeKey(status);
        return normalized switch
        {
            "" => BayStatusEmpty,
            DbBayStatusActive or "AVAILABLE" or "READY" or "EMPTY" or "FREE" or "TRONG" => BayStatusEmpty,
            "OCCUPIED" or "IN_USE" or "BUSY" or "USING" or "DANG_SU_DUNG" => BayStatusInUse,
            DbBayStatusUnderMaintenance or DbBayStatusBroken or "MAINTENANCE" or "REPAIR" or "BAO_TRI" => BayStatusMaintenance,
            "SUSPENDED" or "PAUSED" or "TEMP_STOP" or "TAM_NGUNG" => BayStatusSuspended,
            _ => normalized
        };
    }

    internal static bool IsSegmentCompatible(string normalizedSegment, IReadOnlyList<string> supportedSegments)
    {
        if (supportedSegments.Count == 0) return true;
        if (string.IsNullOrWhiteSpace(normalizedSegment)) return false;

        var normalizedList = supportedSegments
            .Select(NormalizeSegment)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return normalizedList.Contains("ALL") || normalizedList.Contains(normalizedSegment);
    }

    internal static string NormalizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToUpperInvariant().Replace(" ", "_");
    }

    internal static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToUpperInvariant();
    }

    internal static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToUpperInvariant();
    }

    internal static string? NormalizeNullableText(string? value)
    {
        if (value == null) return null;
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    internal static string MergeNote(string? oldValue, string? newValue)
    {
        var oldText = string.IsNullOrWhiteSpace(oldValue) ? "" : oldValue.Trim();
        var newText = string.IsNullOrWhiteSpace(newValue) ? "" : newValue.Trim();
        if (string.IsNullOrWhiteSpace(newText)) return oldText;
        if (string.IsNullOrWhiteSpace(oldText)) return newText;
        return $"{oldText} | {newText}";
    }
}
