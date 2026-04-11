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
    public partial class ServiceOrderService
    {
        private static readonly HashSet<string> QuoteLockedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ServiceOrderLifecycleStatus.APPROVED),
            nameof(ServiceOrderLifecycleStatus.IN_PROGRESS),
            nameof(ServiceOrderLifecycleStatus.REPAIRING),
            nameof(ServiceOrderLifecycleStatus.READY),
            nameof(ServiceOrderLifecycleStatus.WAITING_PAYMENT),
            nameof(ServiceOrderLifecycleStatus.CLOSED),
            nameof(ServiceOrderLifecycleStatus.CANCELED),
        };

        private static readonly HashSet<string> AssignmentLockedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ServiceOrderLifecycleStatus.IN_PROGRESS),
            nameof(ServiceOrderLifecycleStatus.REPAIRING),
            nameof(ServiceOrderLifecycleStatus.READY),
            nameof(ServiceOrderLifecycleStatus.WAITING_PAYMENT),
            nameof(ServiceOrderLifecycleStatus.CLOSED),
            nameof(ServiceOrderLifecycleStatus.CANCELED),
        };

        private static readonly HashSet<string> StartedAssignmentStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ServiceProgressStatus.IN_PROGRESS),
            nameof(ServiceProgressStatus.PAUSED),
            nameof(ServiceProgressStatus.COMPLETED),
        };

        private static readonly HashSet<string> PartsLockedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(MaterialRequestStatusCode.PARTIAL),
            nameof(MaterialRequestStatusCode.COMPLETED),
        };

        private static readonly HashSet<string> BillingLockedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ServiceOrderLifecycleStatus.WAITING_PAYMENT),
            nameof(ServiceOrderLifecycleStatus.CLOSED),
        };

        private static readonly HashSet<string> BillingPaymentStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(PaymentStatusCode.COMPLETED),
        };

        private static readonly HashSet<string> ClosedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ServiceOrderLifecycleStatus.CLOSED),
            nameof(ServiceOrderLifecycleStatus.CANCELED),
        };

        private const string WorkshopQuoteLockedMessage = "Không thể sửa vì báo giá đã được chốt với khách hàng.";
        private const string WorkshopAssignmentLockedMessage = "Không thể sửa vì đã có thợ bắt đầu thực hiện công việc.";
        private const string WorkshopPartsLockedMessage = "Không thể sửa vì vật tư đã được xác nhận xuất kho.";
        private const string WorkshopBillingLockedMessage = "Không thể sửa vì lệnh đã vào giai đoạn tài chính/quyết toán.";
        private const string WorkshopClosedMessage = "Không thể sửa vì lệnh đã hoàn tất xuất xưởng hoặc đã đóng.";

        private const string EditRequestAction = "REQUEST_EDIT";
        private const string ServiceOrderTableName = "SERVICEORDERS";
        private const string EditRequestStatusApproved = "APPROVED";

        private static readonly JsonSerializerOptions EditRequestJsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<ServiceOrderEditCapabilityResponseDto?> GetEditCapabilityAsync(long id)
        {
            if (id <= 0)
            {
                return null;
            }

            var order = await _context.ServiceOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (order == null)
            {
                return null;
            }

            var normalizedStatus = NormalizeLockStatus(order.Status);
            var isWindowPassed = IsWorkshopEditWindowPassed(order);
            var isQuoteLocked = order.CustomerApprovedAt.HasValue || QuoteLockedStatuses.Contains(normalizedStatus);
            var isAssignmentLocked = AssignmentLockedStatuses.Contains(normalizedStatus) || await HasStartedAssignmentsAsync(id);
            var isPartsLocked = await HasIssuedPartsAsync(id);
            var isBillingLocked = BillingLockedStatuses.Contains(normalizedStatus)
                || IsBillingStatusLocked(order.PaymentStatus)
                || await HasBillingSnapshotAsync(id);
            var isClosedLocked = ClosedStatuses.Contains(normalizedStatus)
                || order.ActualHandover.HasValue
                || await HasGatePassClosedAsync(id);

            var overrideAccess = await ResolveActiveEditOverrideAccessAsync(id, _clock.UtcNow);

            var isWindowBlocked = isWindowPassed && !overrideAccess.CanOverrideWorkshop;
            var isQuoteBlocked = isQuoteLocked && !overrideAccess.CanOverrideQuote;
            var isAssignmentBlocked = isAssignmentLocked && !overrideAccess.CanOverrideAssignment;
            var isPartsBlocked = isPartsLocked && !overrideAccess.CanOverrideParts;
            var isBillingBlocked = isBillingLocked && !overrideAccess.CanOverrideBilling;
            var isClosedBlocked = isClosedLocked && !overrideAccess.CanOverrideClosed;
            var canEditWorkshop = !(isWindowBlocked || isQuoteBlocked || isAssignmentBlocked || isPartsBlocked || isBillingBlocked || isClosedBlocked);

            var capability = new ServiceOrderEditCapabilityResponseDto
            {
                ServiceOrderId = id,
                CanEditWorkshop = canEditWorkshop,
                IsWindowPassed = isWindowPassed,
                IsQuoteLocked = isQuoteLocked,
                IsAssignmentLocked = isAssignmentLocked,
                IsPartsLocked = isPartsLocked,
                IsBillingLocked = isBillingLocked,
                IsClosedLocked = isClosedLocked,
                IsOverrideActive = overrideAccess.IsActive,
                CanOverrideWorkshop = overrideAccess.CanOverrideWorkshop,
                CanOverrideQuote = overrideAccess.CanOverrideQuote,
                CanOverrideAssignment = overrideAccess.CanOverrideAssignment,
                CanOverrideParts = overrideAccess.CanOverrideParts,
                CanOverrideBilling = overrideAccess.CanOverrideBilling,
                CanOverrideClosed = overrideAccess.CanOverrideClosed,
                OverrideRequestId = overrideAccess.RequestId,
                OverrideUntilUtc = overrideAccess.OverrideUntilUtc
            };

            capability.BlockingReason = BuildWorkshopBlockingReason(capability);
            return capability;
        }

        public async Task EnsureWorkshopEditableAsync(long id)
        {
            var capability = await RequireEditCapabilityAsync(id);
            if (!capability.CanEditWorkshop)
            {
                throw new InvalidOperationException(capability.BlockingReason ?? WorkshopEditWindowPassedMessage);
            }
        }

        public async Task EnsureQuoteAndPartsEditableAsync(long id)
        {
            var capability = await RequireEditCapabilityAsync(id);
            var reason = BuildQuoteAndPartsBlockingReason(capability);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException(reason);
            }
        }

        public async Task EnsureAssignmentEditableAsync(long id)
        {
            var capability = await RequireEditCapabilityAsync(id);
            var reason = BuildAssignmentBlockingReason(capability);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException(reason);
            }
        }

        public async Task EnsurePartsEditableAsync(long id)
        {
            var capability = await RequireEditCapabilityAsync(id);
            var reason = BuildPartsBlockingReason(capability);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException(reason);
            }
        }

        private async Task<ServiceOrderEditCapabilityResponseDto> RequireEditCapabilityAsync(long id)
        {
            var capability = await GetEditCapabilityAsync(id);
            if (capability == null)
            {
                throw new InvalidOperationException("Không tìm thấy service order.");
            }

            return capability;
        }

        private async Task<bool> HasStartedAssignmentsAsync(long serviceOrderId)
        {
            var assignmentRows = await (
                from assignment in _context.ServiceStaffAssignments.AsNoTracking()
                join detail in _context.ServiceOrderDetails.AsNoTracking()
                    on assignment.ServiceOrderDetailId equals detail.Id
                where detail.ServiceOrderId == serviceOrderId
                select new
                {
                    assignment.ActualStartTime,
                    assignment.ActualEndTime,
                    assignment.Status
                }).ToListAsync();

            return assignmentRows.Any(row =>
                row.ActualStartTime.HasValue ||
                row.ActualEndTime.HasValue ||
                StartedAssignmentStatuses.Contains(NormalizeLockStatus(row.Status)));
        }

        private async Task<bool> HasIssuedPartsAsync(long serviceOrderId)
        {
            var hasIssuedFromDetails = await _context.ServiceOrderDetails.AsNoTracking()
                .AnyAsync(detail =>
                    detail.ServiceOrderId == serviceOrderId &&
                    detail.IssuedQuantity.HasValue &&
                    detail.IssuedQuantity.Value > 0m);
            if (hasIssuedFromDetails)
            {
                return true;
            }

            var requestRows = await (
                from requestDetail in _context.MaterialRequestDetails.AsNoTracking()
                join request in _context.MaterialRequests.AsNoTracking()
                    on requestDetail.RequestId equals request.Id
                where request.ServiceOrderId == serviceOrderId
                select new
                {
                    requestDetail.IssuedQuantity,
                    requestDetail.Status,
                    RequestStatus = request.Status
                }).ToListAsync();

            return requestRows.Any(row =>
                (row.IssuedQuantity ?? 0m) > 0m ||
                PartsLockedStatuses.Contains(NormalizeLockStatus(row.Status)) ||
                PartsLockedStatuses.Contains(NormalizeLockStatus(row.RequestStatus)));
        }

        private async Task<bool> HasBillingSnapshotAsync(long serviceOrderId)
        {
            var hasIssuedInvoice = await _context.Invoices.AsNoTracking()
                .AnyAsync(invoice =>
                    invoice.ServiceOrderId == serviceOrderId &&
                    (invoice.IssuedAt.HasValue ||
                     (invoice.PaidAmount ?? 0m) > 0m ||
                     (invoice.PaymentStatus != null &&
                      invoice.PaymentStatus != string.Empty &&
                      invoice.PaymentStatus.ToUpper() != nameof(InvoicePaymentStatusCode.UNPAID))));
            if (hasIssuedInvoice)
            {
                return true;
            }

            var paymentStatuses = await _context.Payments.AsNoTracking()
                .Where(payment => payment.ServiceOrderId == serviceOrderId && payment.Status != null)
                .Select(payment => payment.Status!)
                .ToListAsync();

            return paymentStatuses.Any(status => BillingPaymentStatuses.Contains(NormalizeLockStatus(status)));
        }

        private async Task<bool> HasGatePassClosedAsync(long serviceOrderId)
        {
            var gatePassRows = await _context.GatePasses.AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Select(x => new
                {
                    x.Status,
                    x.PassedAt
                })
                .ToListAsync();

            return gatePassRows.Any(row =>
                row.PassedAt.HasValue ||
                string.Equals(
                    NormalizeLockStatus(row.Status),
                    nameof(GatePassStatusCode.SCANNED),
                    StringComparison.OrdinalIgnoreCase));
        }

        private async Task<ServiceOrderOverrideAccess> ResolveActiveEditOverrideAccessAsync(long serviceOrderId, DateTime nowUtc)
        {
            var requestLogs = await _context.AuditLogs
                .AsNoTracking()
                .Where(log =>
                    log.Action == EditRequestAction &&
                    log.TableName == ServiceOrderTableName &&
                    log.RecordId == serviceOrderId)
                .OrderByDescending(log => log.Id)
                .Take(100)
                .ToListAsync();

            var access = new ServiceOrderOverrideAccess();

            foreach (var log in requestLogs)
            {
                var payload = DeserializeEditRequestPayload(log.NewValue);
                if (payload == null)
                {
                    continue;
                }

                var status = NormalizeLockStatus(payload.Status);
                if (!string.Equals(status, EditRequestStatusApproved, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (payload.OverrideUntilUtc.HasValue && payload.OverrideUntilUtc.Value <= nowUtc)
                {
                    continue;
                }

                var section = NormalizeLockStatus(payload.Section);
                if (string.IsNullOrWhiteSpace(section))
                {
                    section = "WORKSHOP";
                }

                access.IsActive = true;
                if (!access.RequestId.HasValue)
                {
                    access.RequestId = log.Id;
                }

                if (payload.OverrideUntilUtc.HasValue &&
                    (!access.OverrideUntilUtc.HasValue || payload.OverrideUntilUtc.Value > access.OverrideUntilUtc.Value))
                {
                    access.OverrideUntilUtc = payload.OverrideUntilUtc;
                }

                switch (section)
                {
                    case "WORKSHOP":
                        access.CanOverrideWorkshop = true;
                        access.CanOverrideQuote = true;
                        access.CanOverrideAssignment = true;
                        access.CanOverrideParts = true;
                        break;
                    case "QUOTE":
                        access.CanOverrideQuote = true;
                        break;
                    case "ASSIGNMENT":
                        access.CanOverrideAssignment = true;
                        break;
                    case "PARTS":
                        access.CanOverrideParts = true;
                        break;
                    case "BILLING":
                    case "FINANCE":
                        access.CanOverrideBilling = true;
                        break;
                    case "CLOSED":
                    case "GATE_PASS":
                        access.CanOverrideClosed = true;
                        break;
                    default:
                        access.CanOverrideWorkshop = true;
                        access.CanOverrideQuote = true;
                        access.CanOverrideAssignment = true;
                        access.CanOverrideParts = true;
                        break;
                }
            }

            return access;
        }

        private static ServiceOrderEditRequestPayload? DeserializeEditRequestPayload(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ServiceOrderEditRequestPayload>(raw, EditRequestJsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsBillingStatusLocked(string? paymentStatus)
        {
            var normalized = NormalizeLockStatus(paymentStatus);
            return string.Equals(normalized, nameof(InvoicePaymentStatusCode.PAID), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, nameof(InvoicePaymentStatusCode.PARTIAL), StringComparison.OrdinalIgnoreCase);
        }

        private static string? BuildWorkshopBlockingReason(ServiceOrderEditCapabilityResponseDto capability)
        {
            if (capability.IsClosedLocked && !capability.CanOverrideClosed) return WorkshopClosedMessage;
            if (capability.IsBillingLocked && !capability.CanOverrideBilling) return WorkshopBillingLockedMessage;
            if (capability.IsPartsLocked && !capability.CanOverrideParts) return WorkshopPartsLockedMessage;
            if (capability.IsAssignmentLocked && !capability.CanOverrideAssignment) return WorkshopAssignmentLockedMessage;
            if (capability.IsQuoteLocked && !capability.CanOverrideQuote) return WorkshopQuoteLockedMessage;
            if (capability.IsWindowPassed && !capability.CanOverrideWorkshop) return WorkshopEditWindowPassedMessage;
            return null;
        }

        private static string? BuildQuoteAndPartsBlockingReason(ServiceOrderEditCapabilityResponseDto capability)
        {
            if (capability.IsClosedLocked && !capability.CanOverrideClosed) return WorkshopClosedMessage;
            if (capability.IsBillingLocked && !capability.CanOverrideBilling) return WorkshopBillingLockedMessage;
            if (capability.IsPartsLocked && !capability.CanOverrideParts) return WorkshopPartsLockedMessage;
            if (capability.IsAssignmentLocked && !capability.CanOverrideAssignment) return WorkshopAssignmentLockedMessage;
            if (capability.IsQuoteLocked && !capability.CanOverrideQuote) return WorkshopQuoteLockedMessage;
            if (capability.IsWindowPassed && !capability.CanOverrideWorkshop) return WorkshopEditWindowPassedMessage;
            return null;
        }

        private static string? BuildAssignmentBlockingReason(ServiceOrderEditCapabilityResponseDto capability)
        {
            if (capability.IsClosedLocked && !capability.CanOverrideClosed) return WorkshopClosedMessage;
            if (capability.IsBillingLocked && !capability.CanOverrideBilling) return WorkshopBillingLockedMessage;
            if (capability.IsAssignmentLocked && !capability.CanOverrideAssignment) return WorkshopAssignmentLockedMessage;
            if (capability.IsWindowPassed && !capability.CanOverrideWorkshop) return WorkshopEditWindowPassedMessage;
            return null;
        }

        private static string? BuildPartsBlockingReason(ServiceOrderEditCapabilityResponseDto capability)
        {
            if (capability.IsClosedLocked && !capability.CanOverrideClosed) return WorkshopClosedMessage;
            if (capability.IsBillingLocked && !capability.CanOverrideBilling) return WorkshopBillingLockedMessage;
            if (capability.IsPartsLocked && !capability.CanOverrideParts) return WorkshopPartsLockedMessage;
            if (capability.IsWindowPassed && !capability.CanOverrideWorkshop) return WorkshopEditWindowPassedMessage;
            return null;
        }

        private static string NormalizeLockStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status)
                ? string.Empty
                : status.Trim().ToUpperInvariant().Replace("-", "_").Replace(" ", "_");
        }

        private sealed class ServiceOrderOverrideAccess
        {
            public bool IsActive { get; set; }
            public bool CanOverrideWorkshop { get; set; }
            public bool CanOverrideQuote { get; set; }
            public bool CanOverrideAssignment { get; set; }
            public bool CanOverrideParts { get; set; }
            public bool CanOverrideBilling { get; set; }
            public bool CanOverrideClosed { get; set; }
            public long? RequestId { get; set; }
            public DateTime? OverrideUntilUtc { get; set; }
        }

        private sealed class ServiceOrderEditRequestPayload
        {
            public string? Section { get; set; }
            public string? Status { get; set; }
            public DateTime? OverrideUntilUtc { get; set; }
        }
    }
}
