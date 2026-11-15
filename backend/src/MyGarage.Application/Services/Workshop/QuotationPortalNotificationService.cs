using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class QuotationPortalNotificationService : IQuotationPortalNotificationService
{
    private readonly IQuotationWorkflowStore _store;
    private readonly IEmailService _emailService;
    private readonly IQuotationRealtimeNotifier _realtimeNotifier;

    public QuotationPortalNotificationService(
        IQuotationWorkflowStore store,
        IEmailService emailService,
        IQuotationRealtimeNotifier realtimeNotifier)
    {
        _store = store;
        _emailService = emailService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task SendQuotationLinkEmailAsync(Quotation quotation, string targetEmail, string publicLink, string? clientNote, DateTime expiresAt)
    {
        var emailSubject = BuildQuotationEmailSubject(quotation);
        var emailBody = BuildQuotationEmailHtml(quotation, publicLink, clientNote, expiresAt);
        await _emailService.SendEmailAsync(targetEmail, emailSubject, emailBody);
    }

    public async Task<QuotationPublicOtpResponseDto> SendDecisionOtpAsync(QuotationAccessLink accessLink, QuotationPublicOtpRequestDto? request = null)
    {
        var quotation = await _store.GetQuotationWithCustomerForOtpAsync(accessLink.QuotationId);

        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.ServiceOrder == null)
        {
            throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
        }

        QuotationValidationHelper.EnsureCustomerDecisionAllowed(quotation, quotation.ServiceOrder);

        if (!string.Equals(quotation.Status, QuotationWorkflowConstants.StatusPendingCustomer, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Báo giá hiện không ở trạng thái chờ khách xác nhận.");
        }

        var targetEmail = QuotationValidationHelper.NormalizeText(request?.Email, 255)
            ?? QuotationValidationHelper.NormalizeText(accessLink.SentToEmail, 255)
            ?? QuotationValidationHelper.NormalizeText(quotation.ServiceOrder?.Customer?.Email, 255);
        if (string.IsNullOrWhiteSpace(targetEmail))
        {
            throw new InvalidOperationException("Không tìm thấy email hợp lệ để gửi OTP.");
        }

        var now = VietnamTime.Now;
        if (accessLink.OtpLastSentAt.HasValue)
        {
            var elapsed = now - accessLink.OtpLastSentAt.Value;
            if (elapsed.TotalSeconds < QuotationWorkflowConstants.OtpResendCooldownSeconds)
            {
                var retryAfter = Math.Max(1, QuotationWorkflowConstants.OtpResendCooldownSeconds - (int)elapsed.TotalSeconds);
                throw new InvalidOperationException($"Vui lòng chờ {retryAfter} giây để gửi lại OTP.");
            }
        }

        var otpCode = GenerateOtpCode();
        accessLink.OtpCodeHash = ComputeTokenHash(otpCode);
        accessLink.OtpExpiresAt = now.AddSeconds(QuotationWorkflowConstants.OtpTtlSeconds);
        accessLink.OtpAttemptCount = 0;
        accessLink.OtpVerifiedAt = null;
        accessLink.OtpLastSentAt = now;
        accessLink.SentToEmail = targetEmail;
        accessLink.UpdatedAt = now;
        accessLink.UpdatedBy = quotation.ServiceOrder?.CustomerId;
        await _store.SaveChangesInTransactionAsync();

        try
        {
            var plate = quotation.ServiceOrder?.Vehicle?.LicensePlate?.Trim();
            var subject = string.IsNullOrWhiteSpace(plate)
                ? "MyGarage mã OTP xác nhận báo giá"
                : $"MyGarage mã OTP xác nhận báo giá xe {plate}";
            var body = BuildOtpEmailHtml(quotation, otpCode, accessLink.OtpExpiresAt.Value);
            await _emailService.SendEmailAsync(targetEmail, subject, body);
        }
        catch
        {
            accessLink.OtpCodeHash = null;
            accessLink.OtpExpiresAt = null;
            accessLink.OtpAttemptCount = 0;
            accessLink.OtpVerifiedAt = null;
            accessLink.UpdatedAt = VietnamTime.Now;
            await _store.SaveChangesInTransactionAsync();
            throw new InvalidOperationException("Không thể gửi OTP. Vui lòng kiểm tra cấu hình email.");
        }

        return new QuotationPublicOtpResponseDto
        {
            Sent = true,
            MaskedEmail = MaskEmail(targetEmail),
            OtpExpiresAt = accessLink.OtpExpiresAt.Value,
            RetryAfterSeconds = QuotationWorkflowConstants.OtpResendCooldownSeconds
        };
    }

    public async Task TryNotifyInsuranceApprovalChainAsync(Quotation quotation, ServiceOrder serviceOrder, bool approved)
    {
        string subject = string.Empty;
        string content = string.Empty;
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var statusText = approved ? "đã duyệt báo giá" : "đã từ chối báo giá";
            var vehiclePlate = serviceOrder.Vehicle?.LicensePlate?.Trim() ?? "-";
            var serviceOrderCode = serviceOrder.Code?.Trim() ?? serviceOrder.Id.ToString(CultureInfo.InvariantCulture);
            subject = approved
                ? $"Khách đã duyệt báo giá - {vehiclePlate}"
                : $"Khách từ chối báo giá - {vehiclePlate}";

            content = $@"
<div style=""font-family:Arial,sans-serif;color:#1a202c;line-height:1.5;"">
  <h3 style=""margin:0 0 10px;color:#1a365d;"">Thông báo tự động từ MyGarage</h3>
  <p>Khách xe <strong>{WebUtility.HtmlEncode(vehiclePlate)}</strong> {statusText}.</p>
  <p>Lệnh sửa chữa: <strong>{WebUtility.HtmlEncode(serviceOrderCode)}</strong><br/>
     Báo giá: <strong>{WebUtility.HtmlEncode(quotation.Code)}</strong></p>
  <p>Trạng thái hiện tại: <strong>{WebUtility.HtmlEncode(quotation.Status)}</strong></p>
</div>";

            var serviceAdvisorEmail = serviceOrder.ServiceAdvisorId.HasValue
                ? await _store.GetStaffEmailAsync(serviceOrder.ServiceAdvisorId.Value)
                : null;

            var roleCodes = new[] { "WORKSHOP_MANAGER", "FOREMAN", "TEAM_LEADER", "STORE_KEEPER" };
            var fallbackRoleHints = new[] { "quan doc", "to truong", "thu kho", "manager", "foreman", "leader", "STORE_KEEPER" };

            var branchStaffCandidates = await _store.GetBranchStaffCandidatesAsync(serviceOrder.ExecuteBranchId);

            var branchStaffEmails = branchStaffCandidates
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Email) &&
                    (
                        roleCodes.Contains((x.RoleCode ?? string.Empty).Trim().ToUpperInvariant()) ||
                        fallbackRoleHints.Any(h =>
                            (x.RoleName ?? string.Empty).Trim().ToLowerInvariant().Contains(h) ||
                            (x.RoleCode ?? string.Empty).Trim().ToLowerInvariant().Contains(h))
                    ))
                .Select(x => x.Email!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(serviceAdvisorEmail))
            {
                recipients.Add(serviceAdvisorEmail.Trim());
            }

            foreach (var email in branchStaffEmails.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                recipients.Add(email.Trim());
            }

            foreach (var recipient in recipients)
            {
                await _emailService.SendEmailAsync(recipient, subject, content);
            }

            var foremanIds = branchStaffCandidates
                .Where(x =>
                    (x.RoleCode ?? string.Empty).Contains("FOREMAN", StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleCode ?? string.Empty).Contains("TEAM_LEADER", StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleName ?? string.Empty).Contains("quản đốc", StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleName ?? string.Empty).Contains("tổ trưởng", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            var storekeeperIds = branchStaffCandidates
                .Where(x =>
                    (x.RoleCode ?? string.Empty).Contains("STORE_KEEPER", StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleCode ?? string.Empty).Contains("WAREHOUSE", StringComparison.OrdinalIgnoreCase) ||
                    (x.RoleName ?? string.Empty).Contains("thủ kho", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            await _realtimeNotifier.NotifyCustomerDecisionAsync(new QuotationCustomerDecisionRealtimeEvent(
                approved,
                quotation.Id,
                quotation.Code,
                serviceOrder.Id,
                serviceOrder.Code ?? string.Empty,
                vehiclePlate,
                quotation.CustomerDecisionAt,
                serviceOrder.ExecuteBranchId,
                serviceOrder.ServiceAdvisorId,
                foremanIds,
                storekeeperIds));
        }
        catch
        {
            await EnqueueNotificationRetryAsync(quotation, serviceOrder, subject, content, recipients.ToList());
        }
    }

    public async Task NotifyCancellationAlertAsync(Quotation quotation, QuotationDetail canceledDetail, long actorStaffId, DateTime occurredAt)
    {
        if (quotation == null || canceledDetail == null)
        {
            return;
        }

        var serviceOrder = await _store.GetCancellationOrderInfoAsync(quotation.ServiceOrderId);
        if (serviceOrder == null)
        {
            return;
        }

        var recipientIds = new HashSet<long>();
        if (serviceOrder.ReceptionistId.HasValue && serviceOrder.ReceptionistId.Value > 0)
        {
            recipientIds.Add(serviceOrder.ReceptionistId.Value);
        }
        if (serviceOrder.ServiceAdvisorId.HasValue && serviceOrder.ServiceAdvisorId.Value > 0)
        {
            recipientIds.Add(serviceOrder.ServiceAdvisorId.Value);
        }
        recipientIds.Remove(actorStaffId);

        if (recipientIds.Count == 0)
        {
            return;
        }

        await _realtimeNotifier.NotifySupplementaryReductionSavedAsync(
            recipientIds.ToList(),
            new SupplementaryReductionRealtimeEvent(
                serviceOrder.Id,
                serviceOrder.Code,
                quotation.Id,
                canceledDetail.Id,
                canceledDetail.ItemName,
                canceledDetail.CancelReason,
                occurredAt));
    }

    private async Task EnqueueNotificationRetryAsync(Quotation quotation, ServiceOrder serviceOrder, string subject, string content, IReadOnlyList<string> recipients)
    {
        try
        {
            var sanitizedRecipients = recipients
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (sanitizedRecipients.Count == 0)
            {
                return;
            }

            var payload = JsonSerializer.Serialize(new
            {
                subject,
                content,
                recipients = sanitizedRecipients,
                retryCount = 0,
                maxRetries = 5,
                quotationId = quotation.Id,
                serviceOrderId = serviceOrder.Id
            });

            _store.AddAuditLog(new AuditLog
            {
                UserId = quotation.UpdatedBy ?? quotation.CreatedBy ?? 0,
                UserType = "SYSTEM",
                BranchId = serviceOrder.ExecuteBranchId,
                Action = "NOTIFY_RETRY_PENDING",
                TableName = "QUOTATIONS",
                RecordId = quotation.Id,
                NewValue = payload,
                Timestamp = VietnamTime.Now
            });

            await _store.SaveChangesInTransactionAsync();
        }
        catch
        {
            // Do not block workflow when retry queue logging fails.
        }
    }

    private static string GenerateOtpCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString($"D{QuotationWorkflowConstants.OtpLength}", CultureInfo.InvariantCulture);
    }

    private static string ComputeTokenHash(string token)
    {
        var normalized = (token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static string BuildQuotationEmailSubject(Quotation quotation)
    {
        var plate = quotation.ServiceOrder?.Vehicle?.LicensePlate?.Trim();
        var customer = quotation.ServiceOrder?.Customer?.FullName?.Trim();
        var prefix = string.IsNullOrWhiteSpace(plate) ? string.Empty : $" xe {plate}";
        var suffix = string.IsNullOrWhiteSpace(customer) ? string.Empty : $" - KH: {customer}";
        return $"MyGarage Báo giá dịch vụ sửa chữa{prefix}{suffix}";
    }

    private static string BuildQuotationEmailHtml(Quotation quotation, string publicLink, string? clientNote, DateTime expiresAt)
    {
        var customer = WebUtility.HtmlEncode(quotation.ServiceOrder?.Customer?.FullName?.Trim() ?? "Quý khách");
        var plate = WebUtility.HtmlEncode(quotation.ServiceOrder?.Vehicle?.LicensePlate?.Trim() ?? "-");
        var amount = quotation.FinalAmount.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"));
        var expiryText = expiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
        var safeNote = string.IsNullOrWhiteSpace(clientNote)
            ? string.Empty
            : $"<p style=\"margin:12px 0;padding:10px 12px;background:#f7fafc;border-left:4px solid #2b6cb0;\"><strong>Ghi chú từ CVDV:</strong> {WebUtility.HtmlEncode(clientNote)}</p>";

        return $@"
<div style=""font-family:Arial,sans-serif;max-width:680px;margin:0 auto;color:#1a202c;line-height:1.5;"">
  <h2 style=""margin:0 0 12px;color:#1a365d;"">MyGarage - Báo giá sửa chữa</h2>
  <p>Kính chào anh/chị <strong>{customer}</strong>, MyGarage xin gửi báo giá sửa chữa cho xe <strong>{plate}</strong>.</p>
  {safeNote}
  <p style=""margin:12px 0;"">Tổng chi phí dự kiến: <strong style=""font-size:18px;color:#2b6cb0;"">{amount} đ</strong></p>
  <p style=""margin:12px 0;"">
    <a href=""{WebUtility.HtmlEncode(publicLink)}"" style=""display:inline-block;padding:12px 18px;background:#2b6cb0;color:#fff;text-decoration:none;border-radius:8px;font-weight:600;"">
      XEM CHI TIẾT & DUYỆT BÁO GIÁ
    </a>
  </p>
  <p style=""margin:12px 0;color:#4a5568;"">Liên kết có hiệu lực đến: <strong>{expiryText}</strong></p>
  <p style=""margin:16px 0 0;color:#4a5568;"">Nếu cần hỗ trợ thêm, vui lòng phản hồi lại email này để CVDV tư vấn ngay.</p>
</div>";
    }

    private static string BuildOtpEmailHtml(Quotation quotation, string otpCode, DateTime expiresAt)
    {
        var customer = WebUtility.HtmlEncode(quotation.ServiceOrder?.Customer?.FullName?.Trim() ?? "Quý khách");
        var plate = WebUtility.HtmlEncode(quotation.ServiceOrder?.Vehicle?.LicensePlate?.Trim() ?? "-");
        var expiryText = expiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
        var amount = quotation.TotalCustomerAmount.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"));

        return $@"
<div style=""font-family:Arial,sans-serif;max-width:680px;margin:0 auto;color:#1a202c;line-height:1.5;"">
  <h2 style=""margin:0 0 12px;color:#1a365d;"">MyGarage - Xác nhận duyệt báo giá</h2>
  <p>Kính chào anh/chị <strong>{customer}</strong>, vui lòng nhập mã OTP bên dưới để xác nhận báo giá cho xe <strong>{plate}</strong>.</p>
  <div style=""margin:14px 0;padding:14px 16px;background:#f0f7ff;border:1px solid #c6dcf8;border-radius:10px;"">
    <p style=""margin:0 0 6px;color:#31577c;font-size:13px;"">Mã OTP</p>
    <p style=""margin:0;font-size:30px;font-weight:800;letter-spacing:8px;color:#0f3b67;"">{WebUtility.HtmlEncode(otpCode)}</p>
  </div>
  <p style=""margin:8px 0;color:#4a5568;"">Tổng phần khách cần thanh toán: <strong>{amount} đ</strong></p>
  <p style=""margin:8px 0;color:#4a5568;"">OTP hết hạn lúc: <strong>{expiryText}</strong></p>
  <p style=""margin:14px 0 0;color:#4a5568;"">Nếu không thực hiện thao tác này, vui lòng bỏ qua email.</p>
</div>";
    }

    private static string MaskEmail(string email)
    {
        var normalized = (email ?? string.Empty).Trim();
        var at = normalized.IndexOf('@');
        if (at <= 1) return normalized;

        var local = normalized[..at];
        var domain = normalized[(at + 1)..];
        var visible = Math.Min(2, local.Length);
        var masked = $"{local[..visible]}{new string('*', Math.Max(1, local.Length - visible))}";
        return $"{masked}@{domain}";
    }
}
