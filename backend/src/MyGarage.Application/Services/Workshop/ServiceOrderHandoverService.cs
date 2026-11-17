using System;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public sealed class ServiceOrderHandoverService : IServiceOrderHandoverService
    {
        private readonly IServiceOrderHandoverQueryStore _queryStore;
        private readonly IServiceOrderTransactionCommandStore _commandStore;
        private readonly IServiceOrderCustomerLifecycleNotifier _customerLifecycleNotifier;
        private readonly IServiceOrderClosureService _closureService;

        public ServiceOrderHandoverService(
            IServiceOrderHandoverQueryStore queryStore,
            IServiceOrderTransactionCommandStore commandStore,
            IServiceOrderCustomerLifecycleNotifier customerLifecycleNotifier,
            IServiceOrderClosureService closureService)
        {
            _queryStore = queryStore;
            _commandStore = commandStore;
            _customerLifecycleNotifier = customerLifecycleNotifier;
            _closureService = closureService;
        }

        public async Task<HandoverReadinessResponseDto?> GetHandoverReadinessAsync(long id)
        {
            var order = await _queryStore.GetServiceOrderWithDetailsAsNoTrackingAsync(id);

            if (order == null) return null;

            var isPaid = string.Equals(order.PaymentStatus, nameof(InvoicePaymentStatusCode.PAID), StringComparison.OrdinalIgnoreCase);
            var isQcPassed = string.Equals(order.Qcstatus, nameof(ServiceOrderQcStatusCode.PASS), StringComparison.OrdinalIgnoreCase);

            // Check if all labor/service details are COMPLETED
            var workDetails = order.ServiceOrderDetails.Where(d =>
                (string.Equals(d.ItemType, "LABOR", StringComparison.OrdinalIgnoreCase) || string.Equals(d.ItemType, "SERVICE", StringComparison.OrdinalIgnoreCase)) &&
                !string.Equals(d.Status, nameof(ServiceOrderDetailStatusCode.CANCELED), StringComparison.OrdinalIgnoreCase)).ToList();
            var areAllTasksCompleted = workDetails.All(d => string.Equals(d.Status, nameof(ServiceOrderDetailStatusCode.COMPLETED), StringComparison.OrdinalIgnoreCase));

            var response = new HandoverReadinessResponseDto
            {
                ServiceOrderId = id,
                IsPaid = isPaid,
                PaymentStatus = order.PaymentStatus ?? "PENDING",
                IsQcPassed = isQcPassed,
                QcStatus = order.Qcstatus ?? "PENDING",
                AreAllTasksCompleted = areAllTasksCompleted
            };

            if (!isPaid) response.BlockingReason = "Đơn hàng chưa được thanh toán đầy đủ.";
            else if (!isQcPassed) response.BlockingReason = "Chưa hoàn tất kiểm tra chất lượng hoặc QC không đạt.";
            else if (!areAllTasksCompleted) response.BlockingReason = "Vẫn còn các hạng mục sửa chữa chưa hoàn thành.";

            return response;
        }

        public async Task<bool> ProcessHandoverAsync(long id, HandoverRequestDto request)
        {
            return await _commandStore.ExecuteInTransactionAsync(async () =>
            {
                var order = await _queryStore.GetServiceOrderForUpdateAsync(id);
                if (order == null) return false;
                if (string.Equals(order.Status, nameof(ServiceOrderLifecycleStatus.CLOSED), StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(order.Status, nameof(ServiceOrderLifecycleStatus.CANCELED), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Không thể bàn giao lệnh sửa chữa đã hủy.");

                var readiness = await GetHandoverReadinessAsync(id);
                if (readiness == null || !readiness.CanHandover)
                    throw new InvalidOperationException(readiness?.BlockingReason ?? "Không thể bàn giao xe vào lúc này.");

                var receiverName = request.ReceiverName?.Trim();
                var receiverPhone = request.ReceiverPhone?.Trim();
                var notes = request.Notes?.Trim();
                if (string.IsNullOrWhiteSpace(receiverName) || receiverName.Length > 255 ||
                    receiverPhone?.Length > 30 || notes?.Length > 2000)
                    throw new InvalidOperationException("Thông tin người nhận hoặc ghi chú bàn giao không hợp lệ.");

                var effectiveStaffId = request.StaffId > 0 ? request.StaffId : (request.HandedOverBy ?? 0);
                var previousStatus = order.Status;
                order.Status = nameof(ServiceOrderLifecycleStatus.CLOSED);
                order.ActualHandover = request.HandoverTime ?? VietnamTime.Now;
                order.HandoverReceiverName = receiverName;
                order.HandoverReceiverPhone = string.IsNullOrWhiteSpace(receiverPhone) ? null : receiverPhone;
                order.HandoverNotes = string.IsNullOrWhiteSpace(notes) ? null : notes;
                order.UpdatedAt = VietnamTime.Now;
                order.UpdatedBy = effectiveStaffId;
                _customerLifecycleNotifier.EnqueueAfterStatusChange(order, previousStatus ?? string.Empty);
                await _commandStore.SaveChangesAsync();
                await _closureService.HandleOrderClosureAsync(order, previousStatus, effectiveStaffId);
                return true;
            }, clearChangeTracker: true);
        }
    }
}
