using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.GatePasses;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class GatePassAndApprovalApplicationService : IGatePassAndApprovalApplicationService
{
    private readonly IGatePassAndApprovalStore _store;
    private readonly ISharedOperationsAuthorizationService _authService;

    public GatePassAndApprovalApplicationService(
        IGatePassAndApprovalStore store,
        ISharedOperationsAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
    }

    public async Task<GatePassItemDto> IssueGatePassAsync(
        IssueGatePassCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (command.ServiceOrderId <= 0)
        {
            throw new ArgumentException("Thiếu ServiceOrderId hợp lệ để tạo phiếu xuất cổng.");
        }

        var (exists, executeBranchId, soStatus) = await _store.GetServiceOrderInfoAsync(command.ServiceOrderId, ct);
        if (!exists)
        {
            throw new InvalidOperationException("Không tìm thấy service order để xử lý phiếu xuất cổng.");
        }

        if (!await _authService.CanManageGatePassesAsync(executeBranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền phát hành phiếu xuất cổng tại chi nhánh này.");
        }

        var isClosedLocked = IsServiceOrderClosedOrLocked(soStatus);
        var mutationCheck = SharedOperationsDomainRules.ValidateGatePassMutation(
            existingStatus: null,
            passedAt: null,
            isServiceOrderClosedLocked: isClosedLocked);

        if (!mutationCheck.IsValid)
        {
            throw new InvalidOperationException(mutationCheck.ErrorMessage ?? "Không thể tạo phiếu xuất cổng.");
        }

        var clearanceType = string.IsNullOrWhiteSpace(command.ClearanceType)
            ? "NORMAL"
            : command.ClearanceType.Trim().ToUpperInvariant();

        var qrCode = string.IsNullOrWhiteSpace(command.QrCode)
            ? $"GP-{command.ServiceOrderId}-{Guid.NewGuid():N}"[..24].ToUpperInvariant()
            : command.QrCode.Trim();

        var initialStatus = command.ApprovedBy.HasValue && command.ApprovedBy.Value > 0
            ? SharedOperationsDomainRules.StatusApproved
            : SharedOperationsDomainRules.StatusPending;

        var entity = new GatePass
        {
            ServiceOrderId = command.ServiceOrderId,
            ClearanceType = clearanceType,
            QrCode = qrCode,
            IssuedBy = actor.UserId!.Value,
            ApprovedBy = command.ApprovedBy,
            ResolutionNotes = string.IsNullOrWhiteSpace(command.ResolutionNotes) ? null : command.ResolutionNotes.Trim(),
            OldPartsPackingListUrl = string.IsNullOrWhiteSpace(command.OldPartsPackingListUrl) ? null : command.OldPartsPackingListUrl.Trim(),
            IsOldPartsChecked = command.IsOldPartsChecked ?? false,
            Status = initialStatus
        };

        await _store.AddGatePassAsync(entity, ct);
        return MapGatePassDto(entity);
    }

    public async Task<GatePassItemDto?> ScanGatePassAsync(
        long gatePassId,
        ScanGatePassCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var entity = await _store.GetGatePassByIdAsync(gatePassId, ct);
        if (entity == null)
        {
            return null;
        }

        var (exists, executeBranchId, soStatus) = await _store.GetServiceOrderInfoAsync(entity.ServiceOrderId, ct);
        if (!await _authService.CanManageGatePassesAsync(exists ? executeBranchId : null, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xác nhận quét phiếu xuất cổng tại chi nhánh này.");
        }

        var isClosedLocked = exists && IsServiceOrderClosedOrLocked(soStatus);
        var mutationCheck = SharedOperationsDomainRules.ValidateGatePassMutation(
            entity.Status,
            entity.PassedAt,
            isClosedLocked);

        if (!mutationCheck.IsValid)
        {
            throw new InvalidOperationException(mutationCheck.ErrorMessage ?? "Không thể quét phiếu xuất cổng.");
        }

        if (!SharedOperationsDomainRules.CanTransitionGatePassStatus(entity.Status, SharedOperationsDomainRules.StatusScanned))
        {
            throw new InvalidOperationException(
                $"Không thể chuyển trạng thái phiếu xuất cổng từ '{entity.Status}' sang '{SharedOperationsDomainRules.StatusScanned}'.");
        }

        var now = VietnamTime.Now;
        entity.Status = SharedOperationsDomainRules.StatusScanned;
        entity.ScannedBy = actor.UserId!.Value;
        entity.SecurityCheckedAt = now;
        entity.PassedAt = now;

        if (command.IsOldPartsChecked.HasValue)
        {
            entity.IsOldPartsChecked = command.IsOldPartsChecked.Value;
        }

        if (!string.IsNullOrWhiteSpace(command.ResolutionNotes))
        {
            entity.ResolutionNotes = command.ResolutionNotes.Trim();
        }

        await _store.SaveGatePassAsync(entity, ct);
        return MapGatePassDto(entity);
    }

    public async Task<ApprovalRequestItemDto> CreateApprovalRequestAsync(
        CreateApprovalRequestCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessApprovalRequestsAsync(command.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo yêu cầu phê duyệt.");
        }

        var targetId = command.GatePassId ?? command.ServiceOrderDiscountId ?? command.MaterialReturnId;
        var validation = SharedOperationsDomainRules.ValidateApprovalRequestInput(
            command.RequestType,
            targetId,
            command.Reason);

        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.ErrorMessage ?? "Dữ liệu yêu cầu phê duyệt không hợp lệ.");
        }

        var now = VietnamTime.Now;
        var entity = new ApprovalRequest
        {
            RequestType = command.RequestType.Trim().ToUpperInvariant(),
            GatePassId = command.GatePassId,
            ServiceOrderDiscountId = command.ServiceOrderDiscountId,
            MaterialReturnId = command.MaterialReturnId,
            RequestedBy = actor.UserId!.Value,
            Status = SharedOperationsDomainRules.ApprovalPending,
            Reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _store.AddApprovalRequestAsync(entity, ct);
        return MapApprovalRequestDto(entity);
    }

    public async Task<ApprovalRequestItemDto?> DecideApprovalRequestAsync(
        long approvalRequestId,
        DecideApprovalRequestCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var entity = await _store.GetApprovalRequestByIdAsync(approvalRequestId, ct);
        if (entity == null)
        {
            return null;
        }

        if (!await _authService.CanDecideApprovalRequestAsync(approvalRequestId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền phê duyệt hoặc từ chối yêu cầu này.");
        }

        var targetStatus = SharedOperationsDomainRules.NormalizeStatus(command.TargetStatus);
        if (!SharedOperationsDomainRules.CanTransitionApprovalStatus(entity.Status, targetStatus))
        {
            throw new InvalidOperationException(
                $"Không thể chuyển trạng thái phê duyệt từ '{entity.Status ?? SharedOperationsDomainRules.ApprovalPending}' sang '{targetStatus}'.");
        }

        if (targetStatus == SharedOperationsDomainRules.ApprovalRejected &&
            string.IsNullOrWhiteSpace(command.RejectReason))
        {
            throw new ArgumentException("Lý do từ chối không được để trống khi từ chối phê duyệt.");
        }

        var now = VietnamTime.Now;
        entity.Status = targetStatus;
        entity.ApproverId = actor.UserId!.Value;
        entity.RejectReason = string.IsNullOrWhiteSpace(command.RejectReason)
            ? entity.RejectReason
            : command.RejectReason.Trim();
        entity.UpdatedAt = now;

        var approveLinkedGatePass = entity.GatePassId.HasValue
            && targetStatus == SharedOperationsDomainRules.ApprovalApproved;

        await _store.SaveApprovalDecisionAsync(entity, approveLinkedGatePass, actor.UserId!.Value, ct);
        return MapApprovalRequestDto(entity);
    }

    private static bool IsServiceOrderClosedOrLocked(string? status)
    {
        var norm = SharedOperationsDomainRules.NormalizeStatus(status);
        return norm is "CLOSED" or "CANCELED" or "LOCKED";
    }

    private static GatePassItemDto MapGatePassDto(GatePass g) =>
        new(
            g.Id,
            g.ServiceOrderId,
            g.ClearanceType,
            g.QrCode,
            g.IssuedBy,
            g.ApprovedBy,
            g.ResolutionNotes,
            g.OldPartsPackingListUrl,
            g.IsOldPartsChecked,
            g.Status,
            g.ScannedBy,
            g.SecurityCheckedAt,
            g.PassedAt
        );

    private static ApprovalRequestItemDto MapApprovalRequestDto(ApprovalRequest r) =>
        new(
            r.Id,
            r.RequestType,
            r.ServiceOrderDiscountId,
            r.GatePassId,
            r.MaterialReturnId,
            r.RequestedBy,
            r.ApproverId,
            r.Status ?? SharedOperationsDomainRules.ApprovalPending,
            r.Reason,
            r.RejectReason,
            r.CreatedAt,
            r.UpdatedAt
        );
}
