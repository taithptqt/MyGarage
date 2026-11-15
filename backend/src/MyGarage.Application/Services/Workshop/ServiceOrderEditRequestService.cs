using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ServiceOrderEditRequestService : IServiceOrderEditRequestService
    {
        private static readonly JsonSerializerOptions EditRequestJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IServiceOrderService _serviceOrderService;
        private readonly IAuditLogService _auditLogService;
        private readonly IServiceOrderDataService _dataService;

        public ServiceOrderEditRequestService(
            IServiceOrderService serviceOrderService,
            IAuditLogService auditLogService,
            IServiceOrderDataService dataService)
        {
            _serviceOrderService = serviceOrderService;
            _auditLogService = auditLogService;
            _dataService = dataService;
        }

        public async Task<IReadOnlyList<ServiceOrderEditRequestItemDto>> GetEditRequestsAsync(long id, string? status)
        {
            var normalizedStatus = NormalizeToken(status);
            var requestLogs = await _dataService.GetEditRequestLogsAsync(id);

            return requestLogs
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
                .Select(item => new ServiceOrderEditRequestItemDto
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
        }

        public async Task<ServiceOrderCommandResult> RequestEditAsync(
            long id,
            long? currentUserId,
            EditRequestCreateDto? dto)
        {
            var capability = await _serviceOrderService.GetEditCapabilityAsync(id);
            if (capability == null)
            {
                return ServiceOrderCommandResult.NotFound("Không tìm thấy service order.");
            }

            if (capability.CanEditWorkshop)
            {
                return ServiceOrderCommandResult.BadRequest("Service order hiện chưa bị khóa, không cần gửi yêu cầu sửa.");
            }

            var reason = (dto?.Reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return ServiceOrderCommandResult.BadRequest("Vui lòng nhập lý do yêu cầu sửa.");
            }

            var actorUserId = currentUserId ?? dto?.RequestedByUserId;
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return ServiceOrderCommandResult.Unauthorized();
            }

            var requestedSection = NormalizeSection(dto?.Section, "WORKSHOP");
            var order = await _serviceOrderService.GetByIdAsync(id);
            var now = VietnamTime.Now;
            var payload = new EditRequestPayload
            {
                Section = requestedSection,
                Reason = reason,
                Status = "PENDING",
                RequestedByUserId = actorUserId.Value,
                RequestedAtUtc = now
            };

            var created = await _auditLogService.AddAsync(new AuditLog
            {
                UserId = actorUserId.Value,
                UserType = "STAFF",
                BranchId = order?.ExecuteBranchId,
                Action = "REQUEST_EDIT",
                TableName = "SERVICEORDERS",
                RecordId = id,
                OldValue = JsonSerializer.Serialize(capability),
                NewValue = JsonSerializer.Serialize(payload),
                Timestamp = now
            });

            return ServiceOrderCommandResult.Ok(new
            {
                message = "Đã ghi nhận yêu cầu sửa service order.",
                requestId = created.Id
            });
        }

        public async Task<ServiceOrderCommandResult> DecideEditRequestAsync(
            long id,
            long requestId,
            long? currentUserId,
            string? claimRoleCode,
            string? claimRoleName,
            EditRequestDecisionDto? dto)
        {
            var decision = NormalizeToken(dto?.Decision);
            if (decision is not ("APPROVE" or "REJECT"))
            {
                return ServiceOrderCommandResult.BadRequest("Decision phải là APPROVE hoặc REJECT.");
            }

            if (!currentUserId.HasValue || currentUserId.Value <= 0)
            {
                return ServiceOrderCommandResult.Unauthorized();
            }

            var actorUserId = currentUserId.Value;
            var requestLog = await _auditLogService.GetByIdAsync(requestId);
            if (requestLog == null ||
                !string.Equals(requestLog.Action, "REQUEST_EDIT", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(requestLog.TableName, "SERVICEORDERS", StringComparison.OrdinalIgnoreCase) ||
                requestLog.RecordId != id)
            {
                return ServiceOrderCommandResult.NotFound("Không tìm thấy yêu cầu sửa cần xử lý.");
            }

            var payload = DeserializeEditRequestPayload(requestLog.NewValue);
            if (payload == null)
            {
                return ServiceOrderCommandResult.BadRequest("Dữ liệu yêu cầu sửa không hợp lệ.");
            }

            var currentStatus = NormalizeToken(payload.Status);
            if (!string.Equals(currentStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceOrderCommandResult.BadRequest("Yêu cầu sửa này đã được xử lý trước đó.");
            }

            var requestedSection = NormalizeSection(payload.Section, "WORKSHOP");
            var (roleCode, roleName) = await ResolveRoleInfoAsync(actorUserId, claimRoleCode, claimRoleName);
            if (!CanApproveServiceOrderSection(requestedSection, roleCode, roleName))
            {
                return ServiceOrderCommandResult.Forbid();
            }

            var note = string.IsNullOrWhiteSpace(dto?.Note) ? null : dto!.Note!.Trim();
            if (decision == "REJECT" && string.IsNullOrWhiteSpace(note))
            {
                return ServiceOrderCommandResult.BadRequest("Vui lòng nhập lý do từ chối yêu cầu sửa.");
            }

            var now = VietnamTime.Now;
            payload.Section = requestedSection;
            payload.Status = decision == "APPROVE" ? "APPROVED" : "REJECTED";
            payload.DecisionNote = note;
            payload.DecidedByUserId = actorUserId;
            payload.DecidedAtUtc = now;
            payload.DecidedByRoleCode = string.IsNullOrWhiteSpace(roleCode) ? null : roleCode;
            payload.DecidedByRoleName = string.IsNullOrWhiteSpace(roleName) ? null : roleName;
            payload.OverrideUntilUtc = decision == "APPROVE" ? now.AddHours(2) : null;

            var beforeDecision = requestLog.NewValue;
            requestLog.NewValue = JsonSerializer.Serialize(payload);
            var decisionLog = new AuditLog
            {
                UserId = actorUserId,
                UserType = "STAFF",
                BranchId = requestLog.BranchId,
                Action = "DECIDE_EDIT_REQUEST",
                TableName = requestLog.TableName,
                RecordId = requestLog.RecordId,
                OldValue = beforeDecision,
                NewValue = requestLog.NewValue,
                Timestamp = now
            };

            await _dataService.DecideEditRequestAsync(requestLog, decisionLog);

            return ServiceOrderCommandResult.Ok(new
            {
                requestId = requestLog.Id,
                decision = payload.Status,
                overrideUntilUtc = payload.OverrideUntilUtc,
                message = decision == "APPROVE"
                    ? "Đã duyệt yêu cầu sửa service order."
                    : "Đã từ chối yêu cầu sửa service order."
            });
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
            string? claimRoleCode,
            string? claimRoleName)
        {
            if (!string.IsNullOrWhiteSpace(claimRoleCode) || !string.IsNullOrWhiteSpace(claimRoleName))
            {
                return (claimRoleCode ?? string.Empty, claimRoleName ?? string.Empty);
            }

            return await _dataService.GetStaffRoleInfoAsync(staffId);
        }

        private static bool CanApproveServiceOrderSection(string section, string roleCode, string roleName)
        {
            var normalizedSection = NormalizeSection(section, "WORKSHOP");
            var isAdmin = IsAdminRole(roleCode, roleName);
            var isManager = IsManagerRole(roleCode, roleName);
            var isWarehouse = IsWarehouseRole(roleCode, roleName);
            var isAccounting = IsAccountingRole(roleCode, roleName);

            return normalizedSection switch
            {
                "PARTS" => isWarehouse || isManager || isAdmin,
                "BILLING" => isAccounting || isAdmin,
                "FINANCE" => isAccounting || isAdmin,
                "CLOSED" => isAdmin,
                "GATE_PASS" => isAdmin,
                "QUOTE" => isManager || isAdmin,
                "ASSIGNMENT" => isManager || isAdmin,
                "WORKSHOP" => isManager || isAdmin,
                _ => isManager || isAdmin,
            };
        }

        private static bool IsAdminRole(string roleCode, string roleName)
            => ContainsAnyKeyword(roleCode, roleName, "ADMIN", "OWNER", "DIRECTOR", "CHU_XUONG", "SUPERADMIN", "SUPER_ADMIN");

        private static bool IsManagerRole(string roleCode, string roleName)
            => ContainsAnyKeyword(roleCode, roleName, "MANAGER", "SERVICE_MANAGER", "FOREMAN", "TEAM_LEAD", "QUAN_DOC");

        private static bool IsWarehouseRole(string roleCode, string roleName)
            => ContainsAnyKeyword(roleCode, roleName, "WAREHOUSE", "STOREKEEPER", "INVENTORY", "THU_KHO");

        private static bool IsAccountingRole(string roleCode, string roleName)
            => ContainsAnyKeyword(roleCode, roleName, "ACCOUNTANT", "FINANCE", "KE_TOAN", "CHIEF_ACCOUNTANT", "CFO");

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
            => ServiceOrderValueHelper.NormalizeToken(value) ?? string.Empty;

        private static string NormalizeSearchToken(string? value)
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
