using System;

namespace MyGarage.Application.GatePasses;

public sealed record IssueGatePassCommand(
    long ServiceOrderId,
    string ClearanceType = "NORMAL",
    string? QrCode = null,
    long? ApprovedBy = null,
    string? ResolutionNotes = null,
    string? OldPartsPackingListUrl = null,
    bool? IsOldPartsChecked = false
);

public sealed record ScanGatePassCommand(
    bool? IsOldPartsChecked = true,
    string? ResolutionNotes = null
);

public sealed record GatePassItemDto(
    long Id,
    long ServiceOrderId,
    string ClearanceType,
    string QrCode,
    long IssuedBy,
    long? ApprovedBy,
    string? ResolutionNotes,
    string? OldPartsPackingListUrl,
    bool? IsOldPartsChecked,
    string Status,
    long? ScannedBy,
    DateTime? SecurityCheckedAt,
    DateTime? PassedAt
);

public sealed record CreateApprovalRequestCommand(
    string RequestType,
    long? GatePassId = null,
    long? ServiceOrderDiscountId = null,
    long? MaterialReturnId = null,
    string? Reason = null,
    long? BranchId = null
);

public sealed record DecideApprovalRequestCommand(
    string TargetStatus,
    string? RejectReason = null
);

public sealed record ApprovalRequestItemDto(
    long Id,
    string RequestType,
    long? ServiceOrderDiscountId,
    long? GatePassId,
    long? MaterialReturnId,
    long RequestedBy,
    long? ApproverId,
    string Status,
    string? Reason,
    string? RejectReason,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);
