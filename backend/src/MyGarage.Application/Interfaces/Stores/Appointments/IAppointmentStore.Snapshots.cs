using System;

namespace MyGarage.Application.Interfaces.Stores.Appointments;

public sealed record StandardServiceLookupItem(long Id, string? Code, string Name, int StandardMinutes);

public sealed record ProductLabelItem(long Id, string? Name, string? Sku);

public sealed record PartDemandSnapshot(long ProductId, decimal RequiredQuantity);

public sealed record BayTimeRangeSnapshot(long BayId, DateTime StartTime, DateTime EndTime);

public sealed record AppointmentLifecycleConfigSnapshot(
    int PreReminderMinutes,
    int LateReminderMinutes,
    int GoldenWarningMinutes,
    int QuickCancelMinutes,
    int FlexibleCancelMinutes);

public sealed record AppointmentFlaggedWorkSlotSnapshot(long AppointmentId, string? Notes);

public sealed record MandatoryPartBomItem(
    long VehicleModelId,
    long ServiceId,
    string? ServiceName,
    long ProductId,
    string? ProductName,
    string? ProductSku,
    decimal Quantity,
    string? Unit);
