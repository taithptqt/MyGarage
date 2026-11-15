using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Hubs;

namespace MyGarage.Services.Realtime;

public sealed class SignalRQuotationRealtimeNotifier : IQuotationRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _chatHubContext;

    public SignalRQuotationRealtimeNotifier(IHubContext<ChatHub> chatHubContext)
    {
        _chatHubContext = chatHubContext;
    }

    public async Task NotifyCustomerDecisionAsync(QuotationCustomerDecisionRealtimeEvent evt)
    {
        var payload = new
        {
            eventType = "QUOTATION_CUSTOMER_DECISION",
            approved = evt.Approved,
            quotationId = evt.QuotationId,
            quotationCode = evt.QuotationCode,
            serviceOrderId = evt.ServiceOrderId,
            serviceOrderCode = evt.ServiceOrderCode,
            vehiclePlate = evt.VehiclePlate,
            decidedAt = evt.DecidedAt
        };

        if (evt.ExecuteBranchId > 0)
        {
            await _chatHubContext.Clients
                .Group($"WorkshopBranch_{evt.ExecuteBranchId}")
                .SendAsync("QuotationWorkflowUpdated", payload);

            await _chatHubContext.Clients
                .Group($"WorkshopBranch_{evt.ExecuteBranchId}")
                .SendAsync("ServiceOrderExecutionReady", new
                {
                    serviceOrderId = evt.ServiceOrderId,
                    serviceOrderCode = evt.ServiceOrderCode,
                    quotationId = evt.QuotationId,
                    quotationCode = evt.QuotationCode,
                    approved = evt.Approved,
                    message = evt.Approved
                        ? "Khách hàng đã duyệt báo giá, sẵn sàng phát lệnh thi công."
                        : "Khách hàng đã từ chối báo giá, cần xử lý lại phương án."
                });
        }

        if (evt.ServiceAdvisorId.HasValue && evt.ServiceAdvisorId.Value > 0)
        {
            await _chatHubContext.Clients
                .Group($"User_{evt.ServiceAdvisorId.Value}")
                .SendAsync("QuotationWorkflowUpdated", payload);

            await _chatHubContext.Clients
                .Group($"User_{evt.ServiceAdvisorId.Value}")
                .SendAsync("ServiceAdvisorNotification", new
                {
                    serviceOrderId = evt.ServiceOrderId,
                    quotationId = evt.QuotationId,
                    approved = evt.Approved,
                    message = evt.Approved
                        ? "Khách đã duyệt báo giá."
                        : "Khách đã từ chối báo giá."
                });
        }

        foreach (var staffId in evt.ForemanIds)
        {
            await _chatHubContext.Clients
                .Group($"User_{staffId}")
                .SendAsync("ForemanDispatchNotification", new
                {
                    serviceOrderId = evt.ServiceOrderId,
                    quotationId = evt.QuotationId,
                    approved = evt.Approved,
                    message = evt.Approved
                        ? "Báo giá đã được khách duyệt, điều phối xe vào khoang thi công."
                        : "Báo giá bị từ chối, tạm dừng điều phối thi công."
                });
        }

        foreach (var staffId in evt.StorekeeperIds)
        {
            await _chatHubContext.Clients
                .Group($"User_{staffId}")
                .SendAsync("StorekeeperDispatchNotification", new
                {
                    serviceOrderId = evt.ServiceOrderId,
                    quotationId = evt.QuotationId,
                    approved = evt.Approved,
                    message = evt.Approved
                        ? "Báo giá đã duyệt, chuẩn bị xuất đúng vật tư theo lệnh."
                        : "Báo giá bị từ chối, giữ nguyên kế hoạch xuất kho."
                });
        }
    }

    public async Task NotifySupplementaryReductionSavedAsync(IReadOnlyList<long> recipientIds, SupplementaryReductionRealtimeEvent evt)
    {
        var payload = new
        {
            eventType = "SupplementaryReductionSaved",
            serviceOrderId = evt.ServiceOrderId,
            serviceOrderCode = evt.ServiceOrderCode,
            quotationId = evt.QuotationId,
            quotationDetailId = evt.QuotationDetailId,
            itemName = evt.ItemName,
            cancelReason = evt.CancelReason,
            occurredAt = evt.OccurredAt
        };

        foreach (var recipientId in recipientIds)
        {
            await _chatHubContext.Clients.Group($"User_{recipientId}")
                .SendAsync("SupplementaryReductionSaved", payload);
        }
    }
}
