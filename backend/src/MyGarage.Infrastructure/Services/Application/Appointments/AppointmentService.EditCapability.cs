using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private const string EditRequestAction = "REQUEST_EDIT";
        private const string AppointmentTableName = "APPOINTMENTS";
        private const string EditRequestStatusApproved = "APPROVED";
        private static readonly HashSet<string> ReceptionMilestoneLockedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(AppointmentStatus.ARRIVED),
            nameof(AppointmentStatus.ARRIVED_LATE),
        };

        private static readonly JsonSerializerOptions EditRequestJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<AppointmentEditCapabilityResponseDto?> GetEditCapabilityAsync(long id)
        {
            if (id <= 0)
            {
                return null;
            }

            var appointment = await _context.Appointments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
            if (appointment == null)
            {
                return null;
            }

            var normalizedStatus = NormalizeToken(appointment.Status);
            var isStatusLocked = !ReceptionEditableStatuses.Contains(normalizedStatus);
            var isWindowPassed = ReceptionMilestoneLockedStatuses.Contains(normalizedStatus);
            var overrideState = await ResolveActiveAppointmentOverrideAsync(id, _clock.UtcNow);
            var canEdit = overrideState.IsActive || (!isStatusLocked && !isWindowPassed);

            var blockingReason = canEdit
                ? null
                : isWindowPassed
                ? ReceptionReplaceWindowPassedMessage
                : (isStatusLocked ? ReceptionReplaceStatusLockedMessage : null);

            return new AppointmentEditCapabilityResponseDto
            {
                AppointmentId = appointment.Id,
                CanEdit = canEdit,
                IsStatusLocked = isStatusLocked,
                IsWindowPassed = isWindowPassed,
                IsOverrideActive = overrideState.IsActive,
                OverrideRequestId = overrideState.RequestId,
                OverrideUntilUtc = overrideState.OverrideUntilUtc,
                BlockingReason = blockingReason
            };
        }

        private async Task<AppointmentOverrideState> ResolveActiveAppointmentOverrideAsync(long appointmentId, DateTime nowUtc)
        {
            var requestLogs = await _context.AuditLogs
                .AsNoTracking()
                .Where(log =>
                    log.Action == EditRequestAction &&
                    log.TableName == AppointmentTableName &&
                    log.RecordId == appointmentId)
                .OrderByDescending(log => log.Id)
                .Take(50)
                .ToListAsync();

            foreach (var log in requestLogs)
            {
                var payload = DeserializeEditRequestPayload(log.NewValue);
                if (payload == null)
                {
                    continue;
                }

                var status = NormalizeToken(payload.Status);
                if (!string.Equals(status, EditRequestStatusApproved, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var section = NormalizeToken(payload.Section);
                if (section != "APPOINTMENT" && section != "WORKSHOP")
                {
                    continue;
                }

                if (payload.OverrideUntilUtc.HasValue && payload.OverrideUntilUtc.Value <= nowUtc)
                {
                    continue;
                }

                return new AppointmentOverrideState
                {
                    IsActive = true,
                    RequestId = log.Id,
                    OverrideUntilUtc = payload.OverrideUntilUtc
                };
            }

            return new AppointmentOverrideState();
        }

        private static AppointmentEditRequestPayload? DeserializeEditRequestPayload(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<AppointmentEditRequestPayload>(raw, EditRequestJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeToken(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant().Replace("-", "_").Replace(" ", "_");
        }

        private sealed class AppointmentOverrideState
        {
            public bool IsActive { get; set; }
            public long? RequestId { get; set; }
            public DateTime? OverrideUntilUtc { get; set; }
        }

        private sealed class AppointmentEditRequestPayload
        {
            public string? Section { get; set; }
            public string? Status { get; set; }
            public DateTime? OverrideUntilUtc { get; set; }
        }
    }
}
