using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public class GroupSessionService : IGroupSessionService
{
    private readonly IGroupSessionStore _store;
    private readonly IGroupSessionStatusPolicy _groupSessionStatusPolicy;
    private readonly ISystemClock _clock;

    public GroupSessionService(
        IGroupSessionStore store,
        IGroupSessionStatusPolicy groupSessionStatusPolicy,
        ISystemClock clock)
    {
        _store = store;
        _groupSessionStatusPolicy = groupSessionStatusPolicy;
        _clock = clock;
    }

    public async Task<long?> ResolveStaffActorIdAsync(long? actorUserId)
    {
        if (!actorUserId.HasValue || actorUserId.Value <= 0)
        {
            return null;
        }

        var staffExists = await _store.StaffExistsAsync(actorUserId.Value);
        return staffExists ? actorUserId.Value : null;
    }

    public async Task<IEnumerable<GroupSessionResponseDto>> GetByCustomerAsync(long customerId)
    {
        var groups = await _store.GetGroupSessionsByCustomerAsync(customerId);
        return groups.Select(GroupSessionMappingHelper.MapHeaderOnly);
    }

    public async Task<GroupSessionResponseDto?> GetByCustomerAndIdAsync(long customerId, long GroupSessionId)
    {
        var group = await _store.GetGroupSessionByCustomerAndIdAsync(customerId, GroupSessionId);
        return await GetGroupDetailCoreAsync(group);
    }

    public async Task<IEnumerable<GroupSessionResponseDto>> GetByBranchAsync(long branchId)
    {
        var groups = await _store.GetGroupSessionsByBranchAsync(branchId);
        return groups.Select(GroupSessionMappingHelper.MapHeaderOnly);
    }

    public async Task<GroupSessionResponseDto?> GetByBranchAndIdAsync(long branchId, long GroupSessionId)
    {
        var group = await _store.GetGroupSessionByBranchAndIdAsync(branchId, GroupSessionId);
        return await GetGroupDetailCoreAsync(group);
    }

    public async Task<GroupSession> CreateAsync(long customerId, long branchId, string? customerNote, long? actorStaffId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var entity = new GroupSession
        {
            GroupCode = $"BG-{_clock.UtcNow:yyyyMMddHHmmss}-{suffix}",
            CustomerId = customerId,
            BranchId = branchId,
            Status = nameof(BookingGroupStatus.PENDING),
            TotalAppointments = 0,
            ConfirmedAppointments = 0,
            CanceledAppointments = 0,
            CustomerNote = string.IsNullOrWhiteSpace(customerNote) ? null : customerNote.Trim(),
            CreatedAt = _clock.UtcNow,
            UpdatedAt = _clock.UtcNow,
            CreatedBy = actorStaffId,
            UpdatedBy = actorStaffId
        };

        await _store.AddGroupSessionAsync(entity);
        return entity;
    }

    public async Task SyncCountersAsync(long? groupSessionId, long? actorStaffId = null)
    {
        if (!groupSessionId.HasValue)
        {
            return;
        }

        var group = await _store.GetGroupSessionForUpdateAsync(groupSessionId.Value);
        if (group == null)
        {
            return;
        }

        var statuses = await _store.GetAppointmentStatusesByGroupSessionIdAsync(groupSessionId.Value);
        var outcome = _groupSessionStatusPolicy.Evaluate(statuses, group.Status);
        group.Status = outcome.Status;
        group.TotalAppointments = outcome.TotalAppointments;
        group.ConfirmedAppointments = outcome.ConfirmedAppointments;
        group.CanceledAppointments = outcome.CanceledAppointments;

        var nowUtc = _clock.UtcNow;
        if (outcome.ShouldSetConfirmedAt) group.ConfirmedAt = nowUtc;
        if (outcome.ShouldSetCanceledAt) group.CanceledAt = nowUtc;
        if (outcome.ShouldClearConfirmedAt) group.ConfirmedAt = null;
        if (outcome.ShouldClearCanceledAt) group.CanceledAt = null;
        if (actorStaffId.HasValue) group.UpdatedBy = actorStaffId.Value;

        group.UpdatedAt = nowUtc;
        await _store.SaveChangesAsync();
    }

    public async Task<CancelGroupSessionResponseDto?> CancelAllAsync(long GroupSessionId, string reason, long? actorUserId = null)
    {
        var trimmedReason = (reason ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            throw new InvalidOperationException("Vui lòng nhập lý do hủy nhóm.");
        }

        var actorStaffId = await ResolveStaffActorIdAsync(actorUserId);
        var group = await _store.GetGroupSessionForUpdateAsync(GroupSessionId);
        if (group == null)
        {
            return null;
        }

        var appointments = await _store.GetAppointmentsWithVehicleForUpdateAsync(GroupSessionId);
        if (appointments.Count == 0)
        {
            throw new InvalidOperationException("Nhóm này không có lịch hẹn để hủy.");
        }

        var response = new CancelGroupSessionResponseDto
        {
            GroupSessionId = GroupSessionId,
            TotalAppointments = appointments.Count,
            ConfirmedVehiclePlates = appointments
                .Where(a => GroupSessionMappingHelper.ConfirmedLikeAppointmentStatuses.Contains(a.Status))
                .Select(a => GroupSessionMappingHelper.NormalizeToken(a.Vehicle?.LicensePlate))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        var appointmentIds = appointments.Select(a => a.Id).ToList();
        var serviceOrderRows = await _store.GetServiceOrderStatusesByAppointmentIdsAsync(appointmentIds);
        var serviceOrderStatusMap = serviceOrderRows
            .GroupBy(x => x.AppointmentId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyCollection<string>)g
                    .Select(x => GroupSessionMappingHelper.NormalizeToken(x.Status))
                    .ToList());

        var nowUtc = _clock.UtcNow;
        foreach (var appointment in appointments)
        {
            if (AppointmentStatusGroups.Closed.Contains(appointment.Status))
            {
                continue;
            }

            if (GroupSessionMappingHelper.HasStartedServiceOrder(appointment.Id, serviceOrderStatusMap))
            {
                response.SkippedStartedAppointmentIds.Add(appointment.Id);
                var plate = GroupSessionMappingHelper.NormalizeToken(appointment.Vehicle?.LicensePlate);
                if (!string.IsNullOrWhiteSpace(plate))
                {
                    response.SkippedStartedVehiclePlates.Add(plate);
                }
                continue;
            }

            if (!GroupSessionMappingHelper.GroupCancelableAppointmentStatuses.Contains(appointment.Status))
            {
                continue;
            }

            appointment.Status = nameof(AppointmentStatus.CANCELED);
            appointment.CancelReason = trimmedReason;
            appointment.UpdatedAt = nowUtc;
            if (actorStaffId.HasValue)
            {
                appointment.UpdatedBy = actorStaffId.Value;
            }

            response.CanceledAppointmentIds.Add(appointment.Id);
        }

        response.CanceledCount = response.CanceledAppointmentIds.Count;
        response.SkippedStartedCount = response.SkippedStartedAppointmentIds.Count;
        response.SkippedStartedVehiclePlates = response.SkippedStartedVehiclePlates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (response.CanceledCount <= 0)
        {
            response.GroupStatus = group.Status;
            return response;
        }

        group.CancelReason = trimmedReason;
        group.UpdatedAt = nowUtc;
        if (actorStaffId.HasValue)
        {
            group.UpdatedBy = actorStaffId.Value;
        }

        await _store.AddNotificationAndSaveAsync(new Notification
        {
            CustomerId = group.CustomerId,
            Title = "Cập nhật lịch hẹn nhóm",
            Content = GroupSessionMappingHelper.BuildGroupCancelNotificationContent(response.SkippedStartedCount > 0),
            IsRead = false,
            SentAt = nowUtc,
            LinkAction = $"/group-sessions/{group.Id}"
        });

        await SyncCountersAsync(group.Id, actorStaffId);
        await _store.ReloadGroupSessionAsync(group);

        response.GroupStatus = group.Status;
        return response;
    }

    private async Task<GroupSessionResponseDto?> GetGroupDetailCoreAsync(GroupSession? group)
    {
        if (group == null)
        {
            return null;
        }

        var appointments = await _store.GetGroupAppointmentsWithDetailsAsync(group.Id);
        var serviceIds = GroupSessionMappingHelper.ExtractDistinctIds(appointments, a => a.RequestedServiceIds);
        var packageIds = GroupSessionMappingHelper.ExtractDistinctIds(appointments, a => a.RequestedPackageIds);

        var serviceNameMap = serviceIds.Count == 0
            ? new Dictionary<long, string>()
            : await _store.GetStandardServiceNamesByIdsAsync(serviceIds);
        var packageNameMap = packageIds.Count == 0
            ? new Dictionary<long, string>()
            : await _store.GetPackageNamesByIdsAsync(packageIds);

        var dto = GroupSessionMappingHelper.MapHeaderOnly(group);
        dto.Appointments = appointments
            .Select(a => GroupSessionMappingHelper.MapAppointment(a, serviceNameMap, packageNameMap))
            .ToList();
        return dto;
    }
}
