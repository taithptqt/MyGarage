using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentEditRequestService : IAppointmentEditRequestService
    {
        private static readonly JsonSerializerOptions EditRequestJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IAppointmentReadService _appointmentReadService;
        private readonly IAppointmentLifecycleCommandService _appointmentLifecycleService;
        private readonly IAuditLogService _auditLogService;
        private readonly IAppointmentDataService _dataService;

        public AppointmentEditRequestService(
            IAppointmentReadService appointmentReadService,
            IAppointmentLifecycleCommandService appointmentLifecycleService,
            IAuditLogService auditLogService,
            IAppointmentDataService dataService)
        {
            _appointmentReadService = appointmentReadService;
            _appointmentLifecycleService = appointmentLifecycleService;
            _auditLogService = auditLogService;
            _dataService = dataService;
        }

        public async Task<AppointmentsCommandResult<AppointmentEditCapabilityResponseDto>> GetEditCapabilitiesAsync(
            long id,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default)
        {
            var scopeMeta = await _dataService.GetScopeMetaAsync(id, cancellationToken);
            if (!scopeMeta.Found)
            {
                return AppointmentsCommandResult<AppointmentEditCapabilityResponseDto>.NotFound();
            }

            if (!CanAccessAppointment(scopeMeta.CustomerId, scopeMeta.BranchId, actor))
            {
                return AppointmentsCommandResult<AppointmentEditCapabilityResponseDto>.Forbid();
            }

            var capability = await _appointmentLifecycleService.GetEditCapabilityAsync(id);
            if (capability == null)
            {
                return AppointmentsCommandResult<AppointmentEditCapabilityResponseDto>.NotFound();
            }

            return AppointmentsCommandResult<AppointmentEditCapabilityResponseDto>.Ok(capability);
        }

        public async Task<AppointmentsCommandResult<IReadOnlyList<AppointmentEditRequestItemDto>>> GetEditRequestsAsync(
            long id,
            string? status,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default)
        {
            var scopeMeta = await _dataService.GetScopeMetaAsync(id, cancellationToken);
            if (!scopeMeta.Found)
            {
                return AppointmentsCommandResult<IReadOnlyList<AppointmentEditRequestItemDto>>.NotFound();
            }

            if (!actor.IsAdminStaff && scopeMeta.BranchId != actor.CurrentBranchId)
            {
                return AppointmentsCommandResult<IReadOnlyList<AppointmentEditRequestItemDto>>.Forbid();
            }

            var normalizedStatus = NormalizeToken(status);
            var requestLogs = await _dataService.GetEditRequestLogsAsync(id, cancellationToken);

            var result = requestLogs
                .Select(log => new
                {
                    log.Id,
                    log.UserId,
                    log.BranchId,
                    log.Timestamp,
                    Payload = DeserializeEditRequestPayload(log.NewValue)
                })
                .Where(item => item.Payload != null)
                .Where(item => string.IsNullOrWhiteSpace(normalizedStatus) || NormalizeToken(item.Payload!.Status) == normalizedStatus)
                .Select(item => new AppointmentEditRequestItemDto
                {
                    RequestId = item.Id,
                    UserId = item.UserId,
                    BranchId = item.BranchId,
                    Timestamp = item.Timestamp ?? DateTime.UtcNow,
                    Section = item.Payload!.Section,
                    Reason = item.Payload.Reason,
                    Status = item.Payload.Status,
                    RequestedByUserId = item.Payload.RequestedByUserId,
                    RequestedAtUtc = item.Payload.RequestedAtUtc,
                    DecidedByUserId = item.Payload.DecidedByUserId,
                    DecidedAtUtc = item.Payload.DecidedAtUtc,
                    DecisionNote = item.Payload.DecisionNote,
                    DecidedByRoleCode = item.Payload.DecidedByRoleCode,
                    DecidedByRoleName = item.Payload.DecidedByRoleName,
                    OverrideUntilUtc = item.Payload.OverrideUntilUtc
                })
                .ToList();

            return AppointmentsCommandResult<IReadOnlyList<AppointmentEditRequestItemDto>>.Ok(result);
        }

        public async Task<AppointmentsCommandResult> RequestEditAsync(
            long id,
            EditRequestCreateDto? dto,
            AppointmentActorContext actor)
        {
            var appointment = await _appointmentReadService.GetByIdAsync(id);
            if (appointment == null)
            {
                return AppointmentsCommandResult.NotFound("Không tìm thấy lịch hẹn.");
            }

            if (!actor.IsAdminStaff && appointment.BranchId != actor.CurrentBranchId)
            {
                return AppointmentsCommandResult.Forbid();
            }

            var capability = await _appointmentLifecycleService.GetEditCapabilityAsync(id);
            if (capability == null)
            {
                return AppointmentsCommandResult.NotFound("Không tìm thấy lịch hẹn.");
            }

            if (capability.CanEdit)
            {
                return AppointmentsCommandResult.BadRequest("Lịch hẹn hiện chưa bị khóa, không cần gửi yêu cầu sửa.");
            }

            var reason = (dto?.Reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return AppointmentsCommandResult.BadRequest("Vui lòng nhập lý do yêu cầu sửa.");
            }

            if (!actor.CurrentUserId.HasValue || actor.CurrentUserId.Value <= 0)
            {
                return AppointmentsCommandResult.Unauthorized();
            }

            var now = VietnamTime.Now;
            var requestedSection = NormalizeSection(dto?.Section, "APPOINTMENT");
            var payload = new EditRequestPayload
            {
                Section = requestedSection,
                Reason = reason,
                Status = "PENDING",
                RequestedByUserId = actor.CurrentUserId.Value,
                RequestedAtUtc = now
            };

            var created = await _auditLogService.AddAsync(new AuditLog
            {
                UserId = actor.CurrentUserId.Value,
                UserType = "STAFF",
                BranchId = appointment.BranchId,
                Action = "REQUEST_EDIT",
                TableName = "APPOINTMENTS",
                RecordId = id,
                OldValue = JsonSerializer.Serialize(capability),
                NewValue = JsonSerializer.Serialize(payload),
                Timestamp = now
            });

            return AppointmentsCommandResult.Ok(new
            {
                message = "Đã ghi nhận yêu cầu sửa lịch hẹn.",
                requestId = created.Id
            });
        }

        public async Task<AppointmentsCommandResult> DecideEditRequestAsync(
            long id,
            long requestId,
            EditRequestDecisionDto? dto,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default)
        {
            var decision = NormalizeToken(dto?.Decision);
            if (decision is not ("APPROVE" or "REJECT"))
            {
                return AppointmentsCommandResult.BadRequest("Decision phải là APPROVE hoặc REJECT.");
            }

            if (!actor.CurrentUserId.HasValue || actor.CurrentUserId.Value <= 0)
            {
                return AppointmentsCommandResult.Unauthorized();
            }

            var requestLog = await _auditLogService.GetByIdAsync(requestId);
            if (requestLog == null ||
                !string.Equals(requestLog.Action, "REQUEST_EDIT", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(requestLog.TableName, "APPOINTMENTS", StringComparison.OrdinalIgnoreCase) ||
                requestLog.RecordId != id)
            {
                return AppointmentsCommandResult.NotFound("Không tìm thấy yêu cầu sửa cần xử lý.");
            }

            var payload = DeserializeEditRequestPayload(requestLog.NewValue);
            if (payload == null)
            {
                return AppointmentsCommandResult.BadRequest("Dữ liệu yêu cầu sửa không hợp lệ.");
            }

            var currentStatus = NormalizeToken(payload.Status);
            if (!string.Equals(currentStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return AppointmentsCommandResult.BadRequest("Yêu cầu sửa này đã được xử lý trước đó.");
            }

            var requestedSection = NormalizeSection(payload.Section, "APPOINTMENT");
            var (roleCode, roleName) = await ResolveRoleInfoAsync(actor.CurrentUserId.Value, actor, cancellationToken);
            if (!CanApproveAppointmentRequest(roleCode, roleName))
            {
                return AppointmentsCommandResult.Forbid();
            }

            if (!actor.IsAdminStaff && requestLog.BranchId.HasValue && requestLog.BranchId.Value != actor.CurrentBranchId)
            {
                return AppointmentsCommandResult.Forbid();
            }

            var note = string.IsNullOrWhiteSpace(dto?.Note) ? null : dto!.Note!.Trim();
            if (decision == "REJECT" && string.IsNullOrWhiteSpace(note))
            {
                return AppointmentsCommandResult.BadRequest("Vui lòng nhập lý do từ chối yêu cầu sửa.");
            }

            var now = VietnamTime.Now;
            payload.Section = requestedSection;
            payload.Status = decision == "APPROVE" ? "APPROVED" : "REJECTED";
            payload.DecisionNote = note;
            payload.DecidedByUserId = actor.CurrentUserId.Value;
            payload.DecidedAtUtc = now;
            payload.DecidedByRoleCode = string.IsNullOrWhiteSpace(roleCode) ? null : roleCode;
            payload.DecidedByRoleName = string.IsNullOrWhiteSpace(roleName) ? null : roleName;
            payload.OverrideUntilUtc = decision == "APPROVE" ? now.AddHours(2) : null;

            var beforeDecision = requestLog.NewValue;
            requestLog.NewValue = JsonSerializer.Serialize(payload);
            await _dataService.DecideEditRequestInTransactionAsync(
                requestLog,
                beforeDecision,
                actor.CurrentUserId.Value,
                now,
                cancellationToken);

            return AppointmentsCommandResult.Ok(new
            {
                requestId = requestLog.Id,
                decision = payload.Status,
                overrideUntilUtc = payload.OverrideUntilUtc,
                message = decision == "APPROVE"
                    ? "Đã duyệt yêu cầu sửa lịch hẹn."
                    : "Đã từ chối yêu cầu sửa lịch hẹn."
            });
        }

        internal static bool CanAccessAppointment(long customerId, long branchId, AppointmentActorContext actor)
        {
            if (actor.IsStaffUser)
            {
                return actor.IsAdminStaff || branchId == actor.CurrentBranchId;
            }

            return actor.CurrentUserId.HasValue && customerId == actor.CurrentUserId.Value;
        }

        internal static string NormalizeSearchToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var text = value.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Replace('Đ', 'D')
                .ToUpperInvariant()
                .Replace('-', '_')
                .Replace(' ', '_');
        }

        private static EditRequestPayload? DeserializeEditRequestPayload(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<EditRequestPayload>(raw, EditRequestJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private async Task<(string RoleCode, string RoleName)> ResolveRoleInfoAsync(
            long staffId,
            AppointmentActorContext actor,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(actor.ClaimRoleCode) || !string.IsNullOrWhiteSpace(actor.ClaimRoleName))
            {
                return (actor.ClaimRoleCode ?? string.Empty, actor.ClaimRoleName ?? string.Empty);
            }

            return await _dataService.GetStaffRoleInfoAsync(staffId, cancellationToken);
        }

        private static bool CanApproveAppointmentRequest(string roleCode, string roleName)
            => ContainsAnyKeyword(roleCode, roleName, "MANAGER", "SERVICE_MANAGER", "FOREMAN", "ADMIN", "OWNER", "DIRECTOR", "QUAN_DOC");

        private static bool ContainsAnyKeyword(string roleCode, string roleName, params string[] keywords)
        {
            var normalizedCode = NormalizeSearchToken(roleCode);
            var normalizedName = NormalizeSearchToken(roleName);

            foreach (var keyword in keywords)
            {
                var token = NormalizeSearchToken(keyword);
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (normalizedCode.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                    normalizedName.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeSection(string? section, string fallback)
        {
            var token = NormalizeToken(section);
            if (string.IsNullOrWhiteSpace(token))
            {
                token = NormalizeToken(fallback);
            }

            return token;
        }

        private static string NormalizeToken(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim()
                    .ToUpperInvariant()
                    .Replace("-", "_")
                    .Replace(" ", "_");
        }

        private sealed class EditRequestPayload
        {
            public string? Section { get; set; }
            public string? Reason { get; set; }
            public string? Status { get; set; }
            public long? RequestedByUserId { get; set; }
            public DateTime? RequestedAtUtc { get; set; }
            public long? DecidedByUserId { get; set; }
            public DateTime? DecidedAtUtc { get; set; }
            public string? DecisionNote { get; set; }
            public string? DecidedByRoleCode { get; set; }
            public string? DecidedByRoleName { get; set; }
            public DateTime? OverrideUntilUtc { get; set; }
        }
    }
}
