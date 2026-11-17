using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class QuotationCustomerPortalService : IQuotationCustomerPortalService
{
    private readonly IQuotationWorkflowStore _store;
    private readonly IQuotationPortalNotificationService _notificationService;

    public QuotationCustomerPortalService(
        IQuotationWorkflowStore store,
        IQuotationPortalNotificationService notificationService)
    {
        _store = store;
        _notificationService = notificationService;
    }

    internal QuotationCustomerPortalService(
        IQuotationWorkflowStore store,
        IEmailService emailService,
        IQuotationRealtimeNotifier realtimeNotifier)
        : this(store, new QuotationPortalNotificationService(store, store, store, store, emailService, realtimeNotifier))
    {
    }

    public async Task<QuotationCustomerLinkResponseDto> PublishQuotationCustomerLinkAsync(long quotationId, QuotationCustomerLinkPublishRequestDto request)
    {
        if (quotationId <= 0)
        {
            throw new InvalidOperationException("Mã báo giá không hợp lệ.");
        }

        if (request == null || request.ActorStaffId <= 0)
        {
            throw new InvalidOperationException("Thiếu nhân sự gửi báo giá cho khách.");
        }

        var quotation = await _store.GetQuotationFullForPortalAsync(quotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        if (quotation.Status == QuotationWorkflowConstants.StatusSuperseded)
        {
            throw new InvalidOperationException("Báo giá đã bị thay thế.");
        }

        if (quotation.Status == QuotationWorkflowConstants.StatusCustomerApproved ||
            quotation.Status == QuotationWorkflowConstants.StatusCustomerRejected ||
            quotation.Status == QuotationWorkflowConstants.StatusCanceled)
        {
            throw new InvalidOperationException("Báo giá đã kết thúc trạng thái quyết định của khách.");
        }

        var requiresInsuranceApproval = QuotationValidationHelper.IsInsuranceApprovalRequiredForQuotation(quotation);
        var canSendForCustomer =
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusPendingCustomer, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(quotation.Status, QuotationWorkflowConstants.StatusInsuranceApproved, StringComparison.OrdinalIgnoreCase) ||
            (!requiresInsuranceApproval && string.Equals(quotation.Status, QuotationWorkflowConstants.StatusDraft, StringComparison.OrdinalIgnoreCase));
        if (!canSendForCustomer)
        {
            throw new InvalidOperationException("Chỉ được tạo link cho khách sau khi bảo hiểm đã duyệt.");
        }

        if (requiresInsuranceApproval && string.IsNullOrWhiteSpace(quotation.InsuranceApprovalEvidenceUrl))
        {
            throw new InvalidOperationException("Thiếu bằng chứng duyệt bảo hiểm. Không thể tạo link cho khách.");
        }

        var cleanedNote = QuotationValidationHelper.NormalizeText(request.Note, 1000);
        var targetEmail = QuotationValidationHelper.NormalizeText(request.Email, 255)
            ?? QuotationValidationHelper.NormalizeText(quotation.ServiceOrder?.Customer?.Email, 255);
        if (request.SendEmail && string.IsNullOrWhiteSpace(targetEmail))
        {
            throw new InvalidOperationException("Khách hàng chưa có email hợp lệ để gửi báo giá.");
        }

        var now = VietnamTime.Now;
        var previousStatus = quotation.Status;
        var previousInsuranceQuoteState = quotation.ServiceOrder?.InsuranceQuoteState;
        var normalizedIdempotencyKey = QuotationValidationHelper.NormalizeIdempotencyKey(request.IdempotencyKey);

        if (!string.IsNullOrWhiteSpace(normalizedIdempotencyKey))
        {
            var existingActiveLink = await _store.GetLatestActiveAccessLinkForUpdateAsync(quotation.Id, QuotationWorkflowConstants.LinkStatusActive, now);

            if (existingActiveLink != null &&
                !string.IsNullOrWhiteSpace(existingActiveLink.ClientNote) &&
                existingActiveLink.ClientNote.Contains($"idem:{normalizedIdempotencyKey}", StringComparison.Ordinal))
            {
                var existingToken = GenerateDeterministicPublicToken(existingActiveLink);
                return new QuotationCustomerLinkResponseDto
                {
                    QuotationId = quotation.Id,
                    QuotationCode = quotation.Code,
                    Status = quotation.Status,
                    PublicLink = BuildPublicPortalUrl(existingToken),
                    ExpiresAt = existingActiveLink.ExpiresAt,
                    EmailSent = false,
                    SentToEmail = existingActiveLink.SentToEmail
                };
            }
        }

        quotation.Status = QuotationWorkflowConstants.StatusPendingCustomer;
        quotation.CustomerDecisionStatus = QuotationWorkflowConstants.ApprovalPending;
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = request.ActorStaffId;

        if (quotation.ServiceOrder != null)
        {
            quotation.ServiceOrder.InsuranceQuoteState = QuotationWorkflowConstants.StatusPendingCustomer;
            quotation.ServiceOrder.UpdatedAt = now;
            quotation.ServiceOrder.UpdatedBy = request.ActorStaffId;
        }

        if (!string.IsNullOrWhiteSpace(cleanedNote))
        {
            quotation.InternalNotes = string.IsNullOrWhiteSpace(quotation.InternalNotes)
                ? cleanedNote
                : $"{quotation.InternalNotes.Trim()} | {cleanedNote}";
        }

        var expiresAt = ResolvePortalExpiry(quotation.ValidUntil, now);

        var existingActiveLinks = await _store.GetActiveAccessLinksForUpdateAsync(quotation.Id, QuotationWorkflowConstants.LinkStatusActive);

        foreach (var link in existingActiveLinks)
        {
            link.Status = QuotationWorkflowConstants.LinkStatusRevoked;
            link.UpdatedAt = now;
            link.UpdatedBy = request.ActorStaffId;
        }

        var newLink = new QuotationAccessLink
        {
            QuotationId = quotation.Id,
            TokenHash = string.Empty,
            Status = QuotationWorkflowConstants.LinkStatusActive,
            ExpiresAt = expiresAt,
            SentToEmail = targetEmail,
            ClientNote = string.IsNullOrWhiteSpace(normalizedIdempotencyKey)
                ? cleanedNote
                : string.IsNullOrWhiteSpace(cleanedNote)
                    ? $"idem:{normalizedIdempotencyKey}"
                    : $"{cleanedNote} | idem:{normalizedIdempotencyKey}",
            CreatedAt = now,
            CreatedBy = request.ActorStaffId,
            UpdatedAt = now,
            UpdatedBy = request.ActorStaffId
        };

        _store.AddQuotationAccessLink(newLink);
        await _store.SaveChangesInTransactionAsync();

        var rawToken = GenerateDeterministicPublicToken(newLink);
        newLink.TokenHash = ComputeTokenHash(rawToken);
        newLink.UpdatedAt = VietnamTime.Now;
        newLink.UpdatedBy = request.ActorStaffId;
        await _store.SaveChangesInTransactionAsync();

        var publicLink = BuildPublicPortalUrl(rawToken);

        if (request.SendEmail)
        {
            try
            {
                await _notificationService.SendQuotationLinkEmailAsync(quotation, targetEmail!, publicLink, cleanedNote, expiresAt);
            }
            catch
            {
                newLink.Status = QuotationWorkflowConstants.LinkStatusRevoked;
                newLink.UpdatedAt = VietnamTime.Now;
                newLink.UpdatedBy = request.ActorStaffId;

                quotation.Status = previousStatus;
                quotation.UpdatedAt = VietnamTime.Now;
                quotation.UpdatedBy = request.ActorStaffId;

                if (quotation.ServiceOrder != null)
                {
                    quotation.ServiceOrder.InsuranceQuoteState = previousInsuranceQuoteState;
                    quotation.ServiceOrder.UpdatedAt = VietnamTime.Now;
                    quotation.ServiceOrder.UpdatedBy = request.ActorStaffId;
                }

                await _store.SaveChangesInTransactionAsync();
                throw new InvalidOperationException("Không thể gửi email báo giá. Vui lòng kiểm tra cấu hình SMTP.");
            }
        }

        return new QuotationCustomerLinkResponseDto
        {
            QuotationId = quotation.Id,
            QuotationCode = quotation.Code,
            Status = quotation.Status,
            PublicLink = publicLink,
            ExpiresAt = expiresAt,
            EmailSent = request.SendEmail,
            SentToEmail = targetEmail
        };
    }

    public async Task<QuotationPublicViewResponseDto> GetQuotationByPublicTokenAsync(string token)
    {
        var accessLink = await ResolveActiveAccessLinkAsync(token);
        var quotation = await _store.GetQuotationFullForPortalAsync(accessLink.QuotationId);

        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        var now = VietnamTime.Now;
        accessLink.LastAccessedAt = now;
        if (!accessLink.ViewedAt.HasValue)
        {
            accessLink.ViewedAt = now;
            if (string.Equals(quotation.Status, QuotationWorkflowConstants.StatusSentToCustomer, StringComparison.OrdinalIgnoreCase))
            {
                quotation.Status = QuotationWorkflowConstants.StatusPendingCustomer;
                quotation.UpdatedAt = now;
                quotation.UpdatedBy = quotation.UpdatedBy ?? quotation.CreatedBy;
            }
        }

        await _store.SaveChangesInTransactionAsync();
        var supplementaryMetaByDetailId = await QuotationMappingHelper.BuildSupplementaryMetaByQuotationDetailIdAsync(_store, quotation.ServiceOrderId);
        return QuotationMappingHelper.MapPublicQuotationResponse(quotation, supplementaryMetaByDetailId);
    }

    public async Task<QuotationPublicOtpResponseDto> SendDecisionOtpByPublicTokenAsync(string token, QuotationPublicOtpRequestDto? request = null)
    {
        var accessLink = await ResolveActiveAccessLinkAsync(token);
        return await _notificationService.SendDecisionOtpAsync(accessLink, request);
    }

    public async Task<(QuotationAccessLink AccessLink, long QuotationId, long? CustomerId)> ValidatePublicDecisionAsync(string token, string? otpCode)
    {
        var accessLink = await ResolveActiveAccessLinkAsync(token);
        var quotation = await _store.GetQuotationAsNoTrackingAsync(accessLink.QuotationId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Không tìm thấy báo giá.");
        }

        var serviceOrder = await _store.GetServiceOrderAsNoTrackingAsync(quotation.ServiceOrderId);
        if (serviceOrder == null)
        {
            throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
        }

        QuotationValidationHelper.EnsureCustomerDecisionAllowed(quotation, serviceOrder);
        await ValidateOtpForDecisionAsync(accessLink, otpCode);

        return (accessLink, quotation.Id, serviceOrder.CustomerId);
    }

    public async Task MarkPublicDecisionLinkUsedAsync(QuotationAccessLink accessLink, long quotationId, long? customerId, bool approved)
    {
        // Applying a decision may clear the change tracker; reload before marking the link.
        accessLink = await _store.GetAccessLinkByTokenHashForUpdateAsync(accessLink.TokenHash)
            ?? throw new InvalidOperationException("Không tìm thấy liên kết báo giá.");
        var now = VietnamTime.Now;
        accessLink.DecidedAt = now;
        accessLink.DecisionCode = approved ? "APPROVED" : "REJECTED";
        accessLink.Status = QuotationWorkflowConstants.LinkStatusUsed;
        accessLink.OtpVerifiedAt = now;
        accessLink.OtpCodeHash = null;
        accessLink.OtpExpiresAt = null;
        accessLink.UpdatedAt = now;
        accessLink.UpdatedBy = customerId;

        var siblingActiveLinks = await _store.GetSiblingActiveAccessLinksForUpdateAsync(
            quotationId,
            QuotationWorkflowConstants.LinkStatusActive,
            accessLink.Id);
        foreach (var row in siblingActiveLinks)
        {
            row.Status = QuotationWorkflowConstants.LinkStatusRevoked;
            row.UpdatedAt = now;
            row.UpdatedBy = customerId;
        }

        await _store.SaveChangesInTransactionAsync();
    }

    public Task TryNotifyInsuranceApprovalChainAsync(Quotation quotation, ServiceOrder serviceOrder, bool approved)
        => _notificationService.TryNotifyInsuranceApprovalChainAsync(quotation, serviceOrder, approved);

    public Task NotifyCancellationAlertAsync(Quotation quotation, QuotationDetail canceledDetail, long actorStaffId, DateTime occurredAt)
        => _notificationService.NotifyCancellationAlertAsync(quotation, canceledDetail, actorStaffId, occurredAt);

    private static string GenerateDeterministicPublicToken(QuotationAccessLink link)
    {
        var payload = $"{link.QuotationId}:{link.Id}:{link.CreatedAt.Ticks}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private DateTime ResolvePortalExpiry(DateTime? quotationValidUntil, DateTime now)
    {
        var configuredDays = _store.GetPortalTokenExpiryDays();
        var ttlDays = configuredDays.GetValueOrDefault(QuotationWorkflowConstants.DefaultPortalExpiryDays);
        if (ttlDays <= 0)
        {
            ttlDays = QuotationWorkflowConstants.DefaultPortalExpiryDays;
        }

        var byConfig = now.AddDays(ttlDays);
        if (quotationValidUntil.HasValue && quotationValidUntil.Value > now)
        {
            return quotationValidUntil.Value <= byConfig ? quotationValidUntil.Value : byConfig;
        }

        return byConfig;
    }

    private static string ComputeTokenHash(string token)
    {
        var normalized = (token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private string BuildPublicPortalUrl(string token)
    {
        var configuredBase = _store.GetPortalPublicBaseUrl();
        var trimmedBase = configuredBase.TrimEnd('/');
        return $"{trimmedBase}/{token}";
    }

    private async Task ValidateOtpForDecisionAsync(QuotationAccessLink accessLink, string? rawOtpCode)
    {
        var otpCode = (rawOtpCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(otpCode))
        {
            throw new InvalidOperationException("Vui lòng nhập mã OTP trước khi xác nhận.");
        }

        if (string.IsNullOrWhiteSpace(accessLink.OtpCodeHash) || !accessLink.OtpExpiresAt.HasValue)
        {
            throw new InvalidOperationException("Vui lòng bấm gửi OTP để xác thực trước khi duyệt báo giá.");
        }

        var now = VietnamTime.Now;
        if (accessLink.OtpExpiresAt.Value <= now)
        {
            throw new InvalidOperationException("Mã OTP đã hết hạn. Vui lòng gửi lại OTP mới.");
        }

        if (accessLink.OtpAttemptCount >= QuotationWorkflowConstants.OtpMaxAttempts)
        {
            if (!string.Equals(accessLink.Status, QuotationWorkflowConstants.LinkStatusRevoked, StringComparison.OrdinalIgnoreCase))
            {
                accessLink.Status = QuotationWorkflowConstants.LinkStatusRevoked;
                accessLink.OtpCodeHash = null;
                accessLink.OtpExpiresAt = null;
                accessLink.OtpVerifiedAt = null;
                accessLink.UpdatedAt = now;
                await _store.SaveChangesInTransactionAsync();
            }
            throw new InvalidOperationException("OTP đã nhập sai quá số lần cho phép. Liên kết đã bị khóa, vui lòng liên hệ CVDV.");
        }

        var otpHash = ComputeTokenHash(otpCode);
        if (!string.Equals(otpHash, accessLink.OtpCodeHash, StringComparison.OrdinalIgnoreCase))
        {
            accessLink.OtpAttemptCount += 1;
            if (accessLink.OtpAttemptCount >= QuotationWorkflowConstants.OtpMaxAttempts)
            {
                accessLink.Status = QuotationWorkflowConstants.LinkStatusRevoked;
                accessLink.OtpCodeHash = null;
                accessLink.OtpExpiresAt = null;
                accessLink.OtpVerifiedAt = null;
                accessLink.UpdatedAt = now;
                await _store.SaveChangesInTransactionAsync();
                throw new InvalidOperationException("OTP đã nhập sai quá số lần cho phép. Liên kết đã bị khóa, vui lòng liên hệ CVDV.");
            }

            accessLink.UpdatedAt = now;
            await _store.SaveChangesInTransactionAsync();
            throw new InvalidOperationException("Mã OTP không đúng.");
        }
    }

    private async Task<QuotationAccessLink> ResolveActiveAccessLinkAsync(string token)
    {
        var normalizedToken = (token ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedToken))
        {
            throw new InvalidOperationException("Liên kết báo giá không hợp lệ.");
        }

        var tokenHash = ComputeTokenHash(normalizedToken);
        var link = await _store.GetAccessLinkByTokenHashForUpdateAsync(tokenHash);

        if (link == null)
        {
            throw new InvalidOperationException("Liên kết báo giá không tồn tại hoặc đã bị thu hồi.");
        }

        if (!string.Equals(link.Status, QuotationWorkflowConstants.LinkStatusActive, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Liên kết báo giá không còn hiệu lực.");
        }

        if (link.ExpiresAt <= VietnamTime.Now)
        {
            link.Status = QuotationWorkflowConstants.LinkStatusExpired;
            link.UpdatedAt = VietnamTime.Now;
            await _store.SaveChangesInTransactionAsync();
            throw new InvalidOperationException("Báo giá đã hết hiệu lực, vui lòng liên hệ CVDV để cập nhật giá mới.");
        }

        return link;
    }
}
