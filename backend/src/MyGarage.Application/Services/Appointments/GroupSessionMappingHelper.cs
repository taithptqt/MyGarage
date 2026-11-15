using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services;

internal static class GroupSessionMappingHelper
{
    internal static readonly HashSet<string> GroupCancelableAppointmentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(AppointmentStatus.PENDING),
        nameof(AppointmentStatus.LATE),
        nameof(AppointmentStatus.CONFIRMED),
        nameof(AppointmentStatus.ARRIVED),
        nameof(AppointmentStatus.ARRIVED_LATE)
    };

    internal static readonly HashSet<string> ConfirmedLikeAppointmentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(AppointmentStatus.CONFIRMED),
        nameof(AppointmentStatus.ARRIVED),
        nameof(AppointmentStatus.ARRIVED_LATE)
    };

    private static readonly HashSet<string> NotStartedServiceOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(ServiceOrderLifecycleStatus.NEW)
    };

    internal static GroupSessionResponseDto MapHeaderOnly(GroupSession source) => new()
    {
        Id = source.Id,
        GroupCode = source.GroupCode,
        CustomerId = source.CustomerId,
        BranchId = source.BranchId,
        Status = source.Status,
        TotalAppointments = source.TotalAppointments,
        ConfirmedAppointments = source.ConfirmedAppointments,
        CanceledAppointments = source.CanceledAppointments,
        ConfirmedAt = source.ConfirmedAt,
        CanceledAt = source.CanceledAt,
        CancelReason = source.CancelReason,
        CustomerNote = source.CustomerNote,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt
    };

    internal static AppointmentDetailResponseDto MapAppointment(
        Appointment appointment,
        IReadOnlyDictionary<long, string> serviceNameMap,
        IReadOnlyDictionary<long, string> packageNameMap)
    {
        var response = new AppointmentDetailResponseDto
        {
            Id = appointment.Id,
            BranchId = appointment.BranchId,
            BranchName = appointment.Branch?.Name ?? string.Empty,
            CustomerId = appointment.CustomerId,
            CustomerName = appointment.Customer?.FullName ?? string.Empty,
            CustomerPhone = appointment.Customer?.Phone,
            VehicleId = appointment.VehicleId,
            VehicleLicensePlate = appointment.Vehicle?.LicensePlate,
            VehicleModelName = appointment.Vehicle?.Model?.Brand?.Name != null && appointment.Vehicle?.Model?.ModelName != null
                ? $"{appointment.Vehicle.Model.Brand.Name} {appointment.Vehicle.Model.ModelName}"
                : null,
            GroupSessionId = appointment.GroupSessionId,
            GroupSessionCode = appointment.GroupSession?.GroupCode,
            GroupSessionStatus = appointment.GroupSession?.Status,
            CreatedBy = appointment.CreatedBy,
            StartTime = appointment.StartTime,
            EndTime = appointment.EndTime,
            CreatedAt = appointment.CreatedAt,
            RequestedServiceIds = appointment.RequestedServiceIds,
            RequestedPackageIds = appointment.RequestedPackageIds,
            CustomerNotes = appointment.CustomerNotes,
            IsReminderSent = appointment.IsReminderSent,
            Status = appointment.Status,
            CancelReason = appointment.CancelReason,
            UpdatedAt = appointment.UpdatedAt
        };

        foreach (var id in ParseRequestedIds(appointment.RequestedServiceIds))
        {
            response.ServiceNames.Add(serviceNameMap.TryGetValue(id, out var name) ? name : $"#{id}");
        }

        foreach (var id in ParseRequestedIds(appointment.RequestedPackageIds))
        {
            var packageName = packageNameMap.TryGetValue(id, out var name) ? name : $"#{id}";
            response.PackageNames.Add(packageName);
            response.ServiceNames.Add(packageName);
        }

        return response;
    }

    internal static List<long> ExtractDistinctIds(IEnumerable<Appointment> appointments, Func<Appointment, string?> selector) =>
        appointments
            .Select(selector)
            .SelectMany(ParseRequestedIds)
            .Distinct()
            .ToList();

    internal static IEnumerable<long> ParseRequestedIds(string? csvIds)
    {
        if (string.IsNullOrWhiteSpace(csvIds))
        {
            return Enumerable.Empty<long>();
        }

        return csvIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s.Trim(), out var parsed) ? parsed : 0L)
            .Where(v => v > 0)
            .Distinct()
            .ToList();
    }

    internal static bool HasStartedServiceOrder(
        long appointmentId,
        IReadOnlyDictionary<long, IReadOnlyCollection<string>> serviceOrderStatusMap)
    {
        if (!serviceOrderStatusMap.TryGetValue(appointmentId, out var statuses) || statuses.Count == 0)
        {
            return false;
        }

        return statuses.Any(status => string.IsNullOrWhiteSpace(status) || !NotStartedServiceOrderStatuses.Contains(status));
    }

    internal static string NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    internal static string BuildGroupCancelNotificationContent(bool isPartialCancellation) =>
        isPartialCancellation
            ? "Do một số xe trong nhóm không xác định được trạng thái và đã quá giờ hẹn, gara xin phép hủy các xe chưa đến trong nhóm của bạn để điều phối cho xe khác. Vui lòng liên hệ hotline để hỗ trợ."
            : "Do một số xe trong nhóm không xác định được trạng thái và đã quá giờ hẹn, gara xin phép hủy toàn bộ lịch hẹn nhóm của bạn để điều phối cho xe khác. Vui lòng liên hệ hotline để hỗ trợ.";
}
