using System;
using System.Collections.Generic;

namespace MyGarage.Application.Notifications.DTOs;

public sealed record NotificationItemDto(
    long Id,
    long CustomerId,
    string Title,
    string Content,
    bool IsRead,
    DateTime? SentAt,
    string? LinkAction
);

public sealed record NotificationCreateCommand(
    long CustomerId,
    string Title,
    string Content,
    string? LinkAction
);

public sealed record NotificationPageQuery(
    int Page = 1,
    int PageSize = 20,
    bool? UnreadOnly = null
);

public sealed record NotificationPageResult(
    IReadOnlyList<NotificationItemDto> Items,
    int TotalCount,
    int UnreadCount,
    int Page,
    int PageSize
);

public sealed record MaintenanceReminderScheduleCommand(
    long CustomerId,
    long VehicleId,
    long? ServiceOrderId,
    string ReminderType,
    DateTime ScheduledAt,
    int? ReminderMileage,
    string? EstimatedServices,
    string Channel
);

public sealed record MaintenanceReminderItemDto(
    long Id,
    long CustomerId,
    long VehicleId,
    long? ServiceOrderId,
    string ReminderType,
    DateTime ScheduledAt,
    int? ReminderMileage,
    string? EstimatedServices,
    string Channel,
    string Status,
    DateTime? SentAt,
    string? CustomerResponse,
    long? ResultAppointmentId
);

public sealed record MaintenanceReminderPatchStatusCommand(
    string Status,
    string? CustomerResponse = null,
    long? ResultAppointmentId = null
);
