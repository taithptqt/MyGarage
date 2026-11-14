using System;
using System.Collections.Generic;

namespace MyGarage.Application.CustomerPortal.DTOs;

public sealed record CustomerPortalVehicleUpsertCommand(
    string? LicensePlate,
    long ModelId,
    string? Brand,
    string? Model,
    int? ManufactureYear,
    string? Color,
    string? Vin,
    int? CurrentOdo
);

public sealed record CustomerPortalVehicleItemDto(
    long Id,
    string LicensePlate,
    string? Brand,
    string? Model,
    int? ManufactureYear,
    string? Color,
    string? Vin,
    int? CurrentOdo,
    bool IsCurrentOwner
);

public sealed record CustomerPortalDashboardSummary(
    long CustomerId,
    int ActiveOrderCount,
    int UpcomingAppointmentCount,
    int VehicleCount,
    int UnreadNotificationCount
);

public sealed record CustomerPortalTimelineItem(
    string Code,
    string Label,
    string State
);

public sealed record CustomerPortalOrderLinkContext(
    long CustomerId,
    long ServiceOrderId,
    string OrderCode,
    string OrderStatus,
    string StageCode,
    string StageLabel,
    IReadOnlyList<CustomerPortalTimelineItem> Timeline
);
