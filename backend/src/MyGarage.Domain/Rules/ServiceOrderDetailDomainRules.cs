using System;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Domain.Rules;

public sealed class ServiceOrderDetailSourceSnapshot
{
    public string ServiceSource { get; init; } = nameof(ServiceOrderDetailServiceSourceCode.INITIAL);
    public bool IsSupplementary { get; init; }
    public string? SupplementaryReasonCode { get; init; }
    public long? SourceQuotationId { get; init; }
    public long? SupplementaryRequestedByStaffId { get; init; }
    public DateTime? SupplementaryRequestedAt { get; init; }
    public DateTime? CustomerApprovedAt { get; init; }
    public long? QuotationDetailId { get; init; }
}

public static class ServiceOrderDetailDomainRules
{
    public const string DirectSupplementaryCreateForbiddenMessage =
        "Không thể thêm trực tiếp dịch vụ phát sinh trên ServiceOrderDetails. Hãy tạo/duyệt báo giá bổ sung trước.";

    public const string AppliedApprovedDetailMutationForbiddenMessage =
        "Không cho phép sửa/xóa dịch vụ đã duyệt và đã đồng bộ vào SOD. Hãy tạo bản báo giá điều chỉnh (clone/supersede) để thay đổi.";

    public static void EnsureNoDirectSupplementaryCreate(ServiceOrderDetail? entity)
    {
        if (entity == null)
        {
            return;
        }

        var source = (entity.ServiceSource ?? string.Empty).Trim().ToUpperInvariant();
        var isSupplementarySource =
            source == nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN) ||
            source == nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER);

        if (entity.IsSupplementary || isSupplementarySource)
        {
            throw new InvalidOperationException(DirectSupplementaryCreateForbiddenMessage);
        }
    }

    public static void EnsureNoMutationForAppliedApprovedDetail(ServiceOrderDetail? entity)
    {
        if (entity == null)
        {
            return;
        }

        var hasAppliedQuotationLink = entity.QuotationDetailId.HasValue && entity.QuotationDetailId.Value > 0;
        var normalizedStatus = (entity.Status ?? string.Empty).Trim().ToUpperInvariant();
        var isApprovedFromQuotation =
            normalizedStatus == nameof(ServiceOrderDetailStatusCode.APPROVED) &&
            entity.SourceQuotationId.HasValue &&
            entity.SourceQuotationId.Value > 0;

        if (!hasAppliedQuotationLink && !isApprovedFromQuotation)
        {
            return;
        }

        throw new InvalidOperationException(AppliedApprovedDetailMutationForbiddenMessage);
    }

    public static ServiceOrderDetailSourceSnapshot CaptureSourceSnapshot(ServiceOrderDetail? existingItem)
    {
        if (existingItem == null)
        {
            return new ServiceOrderDetailSourceSnapshot();
        }

        return new ServiceOrderDetailSourceSnapshot
        {
            ServiceSource = NormalizeSource(existingItem.ServiceSource),
            IsSupplementary = existingItem.IsSupplementary,
            SupplementaryReasonCode = NormalizeReason(existingItem.SupplementaryReasonCode),
            SourceQuotationId = existingItem.SourceQuotationId,
            SupplementaryRequestedByStaffId = existingItem.SupplementaryRequestedByStaffId,
            SupplementaryRequestedAt = existingItem.SupplementaryRequestedAt,
            CustomerApprovedAt = existingItem.CustomerApprovedAt,
            QuotationDetailId = existingItem.QuotationDetailId
        };
    }

    public static void RestoreSourceSnapshot(ServiceOrderDetail? existingItem, ServiceOrderDetailSourceSnapshot? snapshot)
    {
        if (existingItem == null || snapshot == null)
        {
            return;
        }

        existingItem.ServiceSource = snapshot.ServiceSource;
        existingItem.IsSupplementary = snapshot.IsSupplementary;
        existingItem.SupplementaryReasonCode = snapshot.SupplementaryReasonCode;
        existingItem.SourceQuotationId = snapshot.SourceQuotationId;
        existingItem.SupplementaryRequestedByStaffId = snapshot.SupplementaryRequestedByStaffId;
        existingItem.SupplementaryRequestedAt = snapshot.SupplementaryRequestedAt;
        existingItem.CustomerApprovedAt = snapshot.CustomerApprovedAt;
        existingItem.QuotationDetailId = snapshot.QuotationDetailId;
    }

    public static string NormalizeSource(string? source)
    {
        var normalized = (source ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN) => nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN),
            nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER) => nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER),
            _ => nameof(ServiceOrderDetailServiceSourceCode.INITIAL)
        };
    }

    public static string? NormalizeReason(string? reason)
    {
        var normalized = (reason ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING) => nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING),
            nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST) => nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST),
            _ => null
        };
    }
}
