using MyGarage.Enums;

namespace MyGarage.Services;

internal static class QuotationWorkflowConstants
{
    internal const string StatusDraft = nameof(QuotationStatusCode.DRAFT);
    internal const string StatusWaitingApproval = nameof(QuotationStatusCode.WAITING_APPROVAL);
    internal const string StatusSentToCustomer = nameof(QuotationStatusCode.SENT_TO_CUSTOMER);
    internal const string StatusPendingInsurance = nameof(QuotationStatusCode.PENDING_INSURANCE);
    internal const string StatusInsuranceApproved = nameof(QuotationStatusCode.INSURANCE_APPROVED);
    internal const string StatusPendingCustomer = nameof(QuotationStatusCode.PENDING_CUSTOMER);
    internal const string StatusCustomerApproved = nameof(QuotationStatusCode.CUSTOMER_APPROVED);
    internal const string StatusCustomerRejected = nameof(QuotationStatusCode.CUSTOMER_REJECTED);
    internal const string StatusRejected = nameof(QuotationStatusCode.REJECTED);
    internal const string StatusSuperseded = nameof(QuotationStatusCode.SUPERSEDED);
    internal const string StatusCanceled = nameof(QuotationStatusCode.CANCELED);
    internal const string LegacyStatusCancelled = "CANCELLED";

    internal const string ApprovalPending = nameof(QuotationDetailApprovalStatusCode.PENDING);
    internal const string ApprovalApproved = nameof(QuotationDetailApprovalStatusCode.APPROVED);
    internal const string ApprovalRejected = nameof(QuotationDetailApprovalStatusCode.REJECTED);
    internal const string DetailStatusActive = "ACTIVE";
    internal const string DetailStatusCanceled = "CANCELED";
    internal const string InsuranceChangeTypeNone = "NONE";
    internal const string InsuranceChangeTypeIncrease = "INCREASE";
    internal const string InsuranceChangeTypeDecrease = "DECREASE";
    internal const string LinkStatusActive = "ACTIVE";
    internal const string LinkStatusExpired = "EXPIRED";
    internal const string LinkStatusRevoked = "REVOKED";
    internal const string LinkStatusUsed = "USED";
    internal const int DefaultPortalExpiryDays = 15;
    internal const int OtpLength = 6;
    internal const int OtpTtlSeconds = 60;
    internal const int OtpMaxAttempts = 5;
    internal const int OtpResendCooldownSeconds = 60;
    internal const decimal DefaultMaxDiscountPercentWithoutManager = 5m;
    internal const decimal DefaultHighValueThreshold = 50000000m;
    internal const string TechnicalApprovalPending = "PENDING";
    internal const string TechnicalApprovalApproved = "APPROVED";
    internal const string TechnicalApprovalRejected = "REJECTED";
    internal const string FinancialApprovalNotRequired = "NOT_REQUIRED";
    internal const string FinancialApprovalPending = "PENDING";
    internal const string FinancialApprovalApproved = "APPROVED";
    internal const string FinancialApprovalRejected = "REJECTED";
    internal const string ApprovalActionTypeTechnical = "TECHNICAL";
    internal const string ApprovalActionTypeFinancial = "FINANCIAL";
    internal const string StatusPendingBranchManager = "PENDING_BRANCH_MANAGER";
    internal const string DiscountTypeMembership = "MEMBERSHIP";
    internal const string DiscountTypeManual = "MANUAL";
    internal const string DiscountTypeVoucher = "VOUCHER";
    internal const string DiscountValueTypeAmount = "AMOUNT";
}
