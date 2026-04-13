using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/appointments")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private const long MaxInsuranceAssetBytes = 10 * 1024 * 1024;

        public sealed record AppointmentStatusPatchRequestDto
        {
            public string? Status { get; init; }
            public string? Reason { get; init; }
            public DateTime? ArrivedAt { get; init; }
        }

        private static readonly JsonSerializerOptions EditRequestJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IAppointmentService _appointmentService;
        private readonly IAuditLogService _auditLogService;
        private readonly ICloudStorageService _cloudStorageService;
        private readonly IGarageContext _context;

        public AppointmentsController(
            IAppointmentService appointmentService,
            IAuditLogService auditLogService,
            ICloudStorageService cloudStorageService,
            IGarageContext context)
        {
            _appointmentService = appointmentService;
            _auditLogService = auditLogService;
            _cloudStorageService = cloudStorageService;
            _context = context;
        }

        [HttpGet("slots")]
        public async Task<ActionResult<IEnumerable<AvailableSlotResponseDto>>> GetAvailability(
            [FromQuery] long branchId,
            [FromQuery] DateOnly date,
            [FromQuery] string? requestedServiceIds = null,
            [FromQuery] string? requestedPackageIds = null,
            [FromQuery] string? bookingFlow = null,
            [FromQuery] int vehicleCount = 1,
            [FromQuery] bool isParallel = true)
        {
            var slots = await _appointmentService.GetAvailableSlotsAsync(branchId, date, requestedServiceIds, requestedPackageIds, bookingFlow, vehicleCount, isParallel);
            return Ok(slots);
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}/nearest-slot")]
        public async Task<ActionResult<NearestSlotSuggestionResponseDto>> GetNearestSlot(
            long branchId,
            [FromQuery] DateTime checkInAt,
            [FromQuery] string? requestedServiceIds = null,
            [FromQuery] string? requestedPackageIds = null,
            [FromQuery] string? bookingFlow = null,
            [FromQuery] int vehicleCount = 1,
            [FromQuery] bool isParallel = false,
            [FromQuery] int searchDays = 3)
        {
            try
            {
                var suggestion = await _appointmentService.SuggestNearestSlotAsync(
                    branchId,
                    checkInAt,
                    requestedServiceIds,
                    requestedPackageIds,
                    bookingFlow,
                    vehicleCount,
                    isParallel,
                    searchDays);
                return Ok(suggestion);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("slot-holds")]
        public async Task<ActionResult<SlotHoldResponseDto>> CreateSlotHold([FromBody] SlotHoldRequestDto dto)
        {
            try
            {
                var result = await _appointmentService.CreateSlotHoldAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpDelete("slot-holds/{holdToken}")]
        public async Task<ActionResult> ReleaseSlotHold(string holdToken)
        {
            var released = await _appointmentService.ReleaseSlotHoldAsync(holdToken);
            if (!released)
            {
                return NotFound();
            }
            return NoContent();
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult> GetAll([FromQuery] string? status = null)
        {
            var appointments = await _appointmentService.GetAllDetailedAsync(status);
            return Ok(MapAppointmentsResponse(appointments));
        }

        [Authorize]
        [HttpGet("customers/current")]
        public async Task<ActionResult> GetMine()
        {
            var customerId = this.GetCurrentUserId();
            if (customerId == null) return Unauthorized();

            var appointments = await _appointmentService.GetCustomerAppointmentsAsync(customerId.Value);
            return Ok(MapAppointmentsResponse(appointments, forceCustomerShape: true));
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}")]
        public async Task<ActionResult> GetByBranch(long branchId, [FromQuery] string? status = null)
        {
            var appointments = await _appointmentService.GetBranchAppointmentsAsync(branchId, status);
            return Ok(MapAppointmentsResponse(appointments));
        }

        [Authorize]
        [HttpGet("{id:long}")]
        public async Task<ActionResult> GetById(long id)
        {
            var dto = await _appointmentService.GetDetailedAsync(id);
            if (dto == null) return NotFound();
            return Ok(MapAppointmentResponse(dto));
        }

        [Authorize]
        [HttpGet("{id:long}/edit-capabilities")]
        public async Task<ActionResult<AppointmentEditCapabilityResponseDto>> GetEditCapabilities(long id)
        {
            var capability = await _appointmentService.GetEditCapabilityAsync(id);
            if (capability == null)
            {
                return NotFound();
            }

            return Ok(capability);
        }

        [Authorize]
        [HttpGet("{id:long}/edit-requests")]
        public async Task<IActionResult> GetEditRequests(long id, [FromQuery] string? status = null)
        {
            var normalizedStatus = NormalizeToken(status);

            var requestLogs = await _context.AuditLogs
                .AsNoTracking()
                .Where(log =>
                    log.Action == "REQUEST_EDIT" &&
                    log.TableName == "APPOINTMENTS" &&
                    log.RecordId == id)
                .OrderByDescending(log => log.Id)
                .Take(100)
                .ToListAsync();

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
                .Select(item => new
                {
                    requestId = item.Id,
                    item.UserId,
                    item.BranchId,
                    item.Timestamp,
                    section = item.Payload!.Section,
                    reason = item.Payload.Reason,
                    status = item.Payload.Status,
                    requestedByUserId = item.Payload.RequestedByUserId,
                    requestedAtUtc = item.Payload.RequestedAtUtc,
                    decidedByUserId = item.Payload.DecidedByUserId,
                    decidedAtUtc = item.Payload.DecidedAtUtc,
                    decisionNote = item.Payload.DecisionNote,
                    decidedByRoleCode = item.Payload.DecidedByRoleCode,
                    decidedByRoleName = item.Payload.DecidedByRoleName,
                    overrideUntilUtc = item.Payload.OverrideUntilUtc
                })
                .ToList();

            return Ok(result);
        }

        [Authorize]
        [HttpPost("{id:long}/edit-requests")]
        public async Task<IActionResult> RequestEdit(long id, [FromBody] EditRequestCreateDto? dto)
        {
            var capability = await _appointmentService.GetEditCapabilityAsync(id);
            if (capability == null)
            {
                return NotFound(new { message = "Không tìm th?y l?ch h?n." });
            }

            if (capability.CanEdit)
            {
                return BadRequest(new { message = "L?ch h?n hi?n chua b? khóa, không c?n g?i yêu c?u s?a." });
            }

            var reason = (dto?.Reason ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { message = "Vui lòng nh?p lý do yêu c?u s?a." });
            }

            var actorUserId = this.GetCurrentUserId() ?? dto?.RequestedByUserId;
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var appointment = await _appointmentService.GetByIdAsync(id);
            var now = VietnamTime.Now;
            var requestedSection = NormalizeSection(dto?.Section, "APPOINTMENT");
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
                BranchId = appointment?.BranchId,
                Action = "REQUEST_EDIT",
                TableName = "APPOINTMENTS",
                RecordId = id,
                OldValue = JsonSerializer.Serialize(capability),
                NewValue = JsonSerializer.Serialize(payload),
                Timestamp = now
            });

            return Ok(new
            {
                message = "Đã ghi nhận yêu cầu sửa lịch hẹn.",
                requestId = created.Id
            });
        }

        [Authorize]
        [HttpPatch("{id:long}/edit-requests/{requestId:long}")]
        public async Task<IActionResult> DecideEditRequest(long id, long requestId, [FromBody] EditRequestDecisionDto? dto)
        {
            var decision = NormalizeToken(dto?.Decision);
            if (decision is not ("APPROVE" or "REJECT"))
            {
                return BadRequest(new { message = "Decision phải là APPROVE hoặc REJECT." });
            }

            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0)
            {
                return Unauthorized();
            }

            var requestLog = await _auditLogService.GetByIdAsync(requestId);
            if (requestLog == null ||
                !string.Equals(requestLog.Action, "REQUEST_EDIT", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(requestLog.TableName, "APPOINTMENTS", StringComparison.OrdinalIgnoreCase) ||
                requestLog.RecordId != id)
            {
                return NotFound(new { message = "Không tìm thấy yêu cầu sửa cần xử lý." });
            }

            var payload = DeserializeEditRequestPayload(requestLog.NewValue);
            if (payload == null)
            {
                return BadRequest(new { message = "Dữ liệu yêu cầu sửa không hợp lệ." });
            }

            var currentStatus = NormalizeToken(payload.Status);
            if (!string.Equals(currentStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Yêu cầu sửa này đã được xử lý trước đó." });
            }

            var requestedSection = NormalizeSection(payload.Section, "APPOINTMENT");
            var (roleCode, roleName) = await ResolveRoleInfoAsync(actorUserId.Value);
            if (!CanApproveAppointmentRequest(roleCode, roleName))
            {
                return Forbid();
            }

            var note = string.IsNullOrWhiteSpace(dto?.Note) ? null : dto!.Note!.Trim();
            if (decision == "REJECT" && string.IsNullOrWhiteSpace(note))
            {
                return BadRequest(new { message = "Vui lòng nhập lý do từ chối yêu cầu sửa." });
            }

            var now = VietnamTime.Now;
            payload.Section = requestedSection;
            payload.Status = decision == "APPROVE" ? "APPROVED" : "REJECTED";
            payload.DecisionNote = note;
            payload.DecidedByUserId = actorUserId.Value;
            payload.DecidedAtUtc = now;
            payload.DecidedByRoleCode = string.IsNullOrWhiteSpace(roleCode) ? null : roleCode;
            payload.DecidedByRoleName = string.IsNullOrWhiteSpace(roleName) ? null : roleName;
            payload.OverrideUntilUtc = decision == "APPROVE" ? now.AddHours(2) : null;

            var beforeDecision = requestLog.NewValue;
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                requestLog.NewValue = JsonSerializer.Serialize(payload);
                await _auditLogService.UpdateAsync(requestLog.Id, requestLog);

                await _auditLogService.AddAsync(new AuditLog
                {
                    UserId = actorUserId.Value,
                    UserType = "STAFF",
                    BranchId = requestLog.BranchId,
                    Action = "DECIDE_EDIT_REQUEST",
                    TableName = requestLog.TableName,
                    RecordId = requestLog.RecordId,
                    OldValue = beforeDecision,
                    NewValue = requestLog.NewValue,
                    Timestamp = now
                });

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return Ok(new
            {
                requestId = requestLog.Id,
                decision = payload.Status,
                overrideUntilUtc = payload.OverrideUntilUtc,
                message = decision == "APPROVE"
                    ? "Đã duyệt yêu cầu sửa lịch hẹn."
                    : "Đã từ chối yêu cầu sửa lịch hẹn."
            });
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] BookAppointmentRequestDto dto)
        {
            var authUserId = this.GetCurrentUserId();
            if (authUserId == null) return Unauthorized();

            dto.CreatedBy = authUserId;

            try
            {
                var appointment = await _appointmentService.BookAsync(authUserId.Value, dto);
                var detail = await _appointmentService.GetDetailedAsync(appointment.Id);
                return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, MapAppointmentResponse(detail));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("reception-bookings")]
        public async Task<ActionResult<ReceptionAppointmentSaveResponseDto>> SaveReceptionBooking([FromBody] ReceptionAppointmentSaveRequestDto dto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (actorUserId == null)
            {
                return Unauthorized();
            }

            try
            {
                var result = await _appointmentService.SaveReceptionBookingAsync(actorUserId.Value, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("insurance-assets")]
        public async Task<ActionResult<object>> UploadInsuranceAsset([FromForm] IFormFile file, [FromForm] string? assetType)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Dữ liệu ảnh bảo hiểm không hợp lệ." });
            }

            if (file.Length > MaxInsuranceAssetBytes)
            {
                return BadRequest(new { message = "Ảnh bảo hiểm vượt quá dung lượng 10MB." });
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) ||
                !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chỉ hỗ trợ tệp ảnh cho hồ sơ bảo hiểm." });
            }

            var normalizedAssetType = NormalizeInsuranceAssetType(assetType);
            if (normalizedAssetType == null)
            {
                return BadRequest(new { message = "Loại ảnh bảo hiểm không hợp lệ." });
            }

            try
            {
                var uploaded = await SaveInsuranceAssetFileAsync(file, normalizedAssetType);
                _context.AppointmentInsuranceUploads.Add(new AppointmentInsuranceUpload
                {
                    AssetType = normalizedAssetType,
                    BlobPath = uploaded.BlobPath,
                    FileUrl = uploaded.Url,
                    Status = AppointmentInsuranceUploadStatuses.Staged
                });
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    url = uploaded.Url,
                    assetType = normalizedAssetType
                });
            }
            catch
            {
                return BadRequest(new { message = "Không thể tải ảnh bảo hiểm lên máy chủ." });
            }
        }

        [AllowAnonymous]
        [HttpPost("public-bookings")]
        public async Task<ActionResult> CreateGuest([FromBody] BookAppointmentRequestDto dto)
        {
            if (!dto.CustomerId.HasValue)
            {
                return BadRequest(new { message = "Vui lòng cung c?p CustomerId cho lu?ng guest." });
            }

            try
            {
                dto.CreatedBy = null;
                var appointment = await _appointmentService.BookAsync(dto.CustomerId.Value, dto);
                var detail = await _appointmentService.GetDetailedAsync(appointment.Id);
                return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, MapAppointmentResponse(detail, forceCustomerShape: true));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPatch("{id:long}")]
        public async Task<ActionResult> UpdateStatus(long id, [FromBody] AppointmentStatusPatchRequestDto dto)
        {
            var requested = (dto?.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(requested))
            {
                return BadRequest(new { message = "Status is required." });
            }

            if (!Enum.TryParse<AppointmentPatchAction>(requested, ignoreCase: true, out var action))
            {
                return BadRequest(new { message = "Unsupported status transition." });
            }

            try
            {
                var actorUserId = this.GetCurrentUserId();
                Appointment? appointment = action switch
                {
                    AppointmentPatchAction.CONFIRMED => await _appointmentService.ConfirmAsync(id, actorUserId),
                    AppointmentPatchAction.CANCELED => await _appointmentService.CancelAsync(id, dto?.Reason, actorUserId),
                    AppointmentPatchAction.CHECKED_IN => await _appointmentService.CheckInAsync(id, dto?.ArrivedAt, actorUserId),
                    AppointmentPatchAction.ARRIVED => await _appointmentService.ArriveAsync(id, dto?.ArrivedAt, actorUserId),
                    _ => null
                };

                if (appointment == null)
                {
                    return NotFound();
                }

                var detail = await _appointmentService.GetDetailedAsync(id);
                return Ok(MapAppointmentResponse(detail));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("branches/{branchId:long}/no-show-cycles")]
        public async Task<ActionResult<object>> RunNoShow(long branchId, [FromQuery] DateTime? now = null)
        {
            var affected = await _appointmentService.AutoMarkNoShowsAsync(branchId, now);
            return Ok(new { updated = affected });
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}/health-metrics")]
        public async Task<ActionResult<BranchBookingHealthResponseDto>> GetBookingHealth(long branchId, [FromQuery] DateTime? at = null)
        {
            var health = await _appointmentService.GetBranchBookingHealthAsync(branchId, at);
            return Ok(health);
        }

        [Authorize]
        [HttpGet("branches/{branchId:long}/allocation-metrics")]
        public async Task<ActionResult<DispatchDashboardResponseDto>> GetDispatchDashboard(long branchId, [FromQuery] DateTime? at = null)
        {
            var dashboard = await _appointmentService.GetDispatchDashboardAsync(branchId, at);
            return Ok(dashboard);
        }

        [Authorize]
        [HttpPost("branches/{branchId:long}/allocation-cycles")]
        public async Task<ActionResult<DispatchCycleResponseDto>> CreateDispatchRun(long branchId, [FromQuery] DateTime? at = null)
        {
            var result = await _appointmentService.RunDispatchCycleAsync(branchId, at);
            return Ok(result);
        }

        [Authorize]
        [HttpPost("branches/{branchId:long}/overnight-slot-constraints")]
        public async Task<ActionResult<OvernightLockResponseDto>> CreateOvernightLock(long branchId, [FromBody] OvernightLockRequestDto? dto)
        {
            var result = await _appointmentService.LockOvernightSlotsAsync(branchId, dto?.BusinessDate);
            return Ok(result);
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

        private async Task<(string Url, string BlobPath)> SaveInsuranceAssetFileAsync(IFormFile file, string assetType)
        {
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var folderName = string.Equals(assetType, AppointmentInsuranceAssetTypes.Card, StringComparison.OrdinalIgnoreCase)
                ? "appointments/insurance-cards"
                : "appointments/insurance-damage";
            var fileName = $"{VietnamTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            var blobPath = $"{folderName.Trim('/')}/{fileName}";
            await using var stream = file.OpenReadStream();
            var url = await _cloudStorageService.UploadAsync(
                stream,
                folderName,
                fileName,
                file.ContentType,
                HttpContext.RequestAborted);
            return (url, blobPath);
        }

        private static string? NormalizeInsuranceAssetType(string? assetType)
        {
            var normalized = string.IsNullOrWhiteSpace(assetType)
                ? string.Empty
                : assetType.Trim().ToUpperInvariant();

            return normalized switch
            {
                AppointmentInsuranceAssetTypes.Card => AppointmentInsuranceAssetTypes.Card,
                AppointmentInsuranceAssetTypes.Damage => AppointmentInsuranceAssetTypes.Damage,
                _ => null
            };
        }

        private async Task<(string RoleCode, string RoleName)> ResolveRoleInfoAsync(long staffId)
        {
            var claimRoleCode = this.GetCurrentRoleCode();
            var claimRoleName = this.GetCurrentRoleName();
            if (!string.IsNullOrWhiteSpace(claimRoleCode) || !string.IsNullOrWhiteSpace(claimRoleName))
            {
                return (claimRoleCode ?? string.Empty, claimRoleName ?? string.Empty);
            }

            var roleInfo = await (
                from staff in _context.Staffs.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on staff.RoleId equals role.Id into roleJoin
                from role in roleJoin.DefaultIfEmpty()
                where staff.Id == staffId
                select new
                {
                    role.RoleCode,
                    role.RoleName
                }).FirstOrDefaultAsync();

            return (roleInfo?.RoleCode ?? string.Empty, roleInfo?.RoleName ?? string.Empty);
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
                .Replace('d', 'd')
                .Replace('Ð', 'D')
                .ToUpperInvariant()
                .Replace('-', '_')
                .Replace(' ', '_');
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

        private object MapAppointmentsResponse(IEnumerable<AppointmentDetailResponseDto> appointments, bool forceCustomerShape = false)
        {
            var list = appointments?.ToList() ?? new List<AppointmentDetailResponseDto>();
            if (!ShouldHidePartsFollowUp(forceCustomerShape))
            {
                return list;
            }

            return list.Select(item => item.ToCustomerResponse()).ToList();
        }

        private object? MapAppointmentResponse(AppointmentDetailResponseDto? appointment, bool forceCustomerShape = false)
        {
            if (appointment == null)
            {
                return null;
            }

            return ShouldHidePartsFollowUp(forceCustomerShape)
                ? appointment.ToCustomerResponse()
                : appointment;
        }

        private bool ShouldHidePartsFollowUp(bool forceCustomerShape = false)
        {
            if (forceCustomerShape)
            {
                return true;
            }

            var claimRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (IsCustomerRole(claimRole))
            {
                return true;
            }

            return IsCustomerRole(this.GetCurrentRoleCode()) || IsCustomerRole(this.GetCurrentRoleName());
        }

        private static bool IsCustomerRole(string? roleValue)
        {
            if (string.IsNullOrWhiteSpace(roleValue))
            {
                return false;
            }

            var normalized = NormalizeSearchToken(roleValue);
            return normalized.Contains("CUSTOMER", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("KHACH_HANG", StringComparison.OrdinalIgnoreCase);
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
