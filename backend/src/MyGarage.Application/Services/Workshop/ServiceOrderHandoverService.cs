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

        public ServiceOrderHandoverService(
            IServiceOrderHandoverQueryStore queryStore,
            IServiceOrderTransactionCommandStore commandStore,
            IServiceOrderCustomerLifecycleNotifier customerLifecycleNotifier)
        {
            _queryStore = queryStore;
            _commandStore = commandStore;
            _customerLifecycleNotifier = customerLifecycleNotifier;
        }

        public async Task<HandoverReadinessResponseDto?> GetHandoverReadinessAsync(long id)
        {
            var order = await _queryStore.GetServiceOrderWithDetailsAsNoTrackingAsync(id);

            if (order == null) return null;

            var isPaid = string.Equals(order.PaymentStatus, nameof(InvoicePaymentStatusCode.PAID), StringComparison.OrdinalIgnoreCase);
            var isQcPassed = string.Equals(order.Qcstatus, nameof(ServiceOrderQcStatusCode.PASS), StringComparison.OrdinalIgnoreCase);

            // Check if all labor/service details are COMPLETED
            var workDetails = order.ServiceOrderDetails.Where(d => d.ItemType == "LABOR" || d.ItemType == "SERVICE").ToList();
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
            // Enforcement policy: Must be PAID and QC_PASS
            var readiness = await GetHandoverReadinessAsync(id);
            if (readiness == null || !readiness.CanHandover)
            {
                throw new InvalidOperationException(readiness?.BlockingReason ?? "Không thể bàn giao xe vào lúc này.");
            }

            return await _commandStore.ExecuteInTransactionAsync(async () =>
            {
                var order = await _queryStore.GetServiceOrderForUpdateAsync(id);
                if (order == null) return false;

                var effectiveStaffId = request.StaffId > 0 ? request.StaffId : (request.HandedOverBy ?? 0);

                order.Status = nameof(ServiceOrderLifecycleStatus.CLOSED);
                order.ActualHandover = request.HandoverTime ?? VietnamTime.Now;
                order.UpdatedAt = VietnamTime.Now;
                order.UpdatedBy = effectiveStaffId;

                await _commandStore.SaveChangesAsync();

                _customerLifecycleNotifier.EnqueueAfterStatusChange(order, nameof(ServiceOrderLifecycleStatus.READY));

                return true;
            }, clearChangeTracker: true);
        }
    }
}
