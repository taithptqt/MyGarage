using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class ServiceOrderCustomerLifecycleNotifier : IServiceOrderCustomerLifecycleNotifier
{
    private readonly IGarageContext _context;
    private readonly ISystemClock _clock;

    public ServiceOrderCustomerLifecycleNotifier(IGarageContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public void EnqueueAfterStatusChange(ServiceOrder order, string previousStatus)
    {
        if (order == null) return;
        var next = order.Status?.Trim() ?? "";
        var prev = previousStatus?.Trim() ?? "";
        if (string.Equals(next, prev, StringComparison.OrdinalIgnoreCase)) return;

        static bool IsTarget(string s) =>
            string.Equals(s, "READY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "WAITING_PAYMENT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s, "CLOSED", StringComparison.OrdinalIgnoreCase);

        if (!IsTarget(next)) return;

        var (title, content) = next.ToUpperInvariant() switch
        {
            "READY" => ("Xe sẵn sàng giao",
                $"Lệnh sửa chữa {order.Code}: xe đã hoàn tất công đoạn sửa chữa, sẵn sàng bàn giao."),
            "WAITING_PAYMENT" => ("Chờ thanh toán",
                $"Lệnh {order.Code}: vui lòng hoàn tất thanh toán để nhận xe."),
            "CLOSED" => ("Dịch vụ hoàn tất",
                $"Lệnh {order.Code}: đã đóng. Cảm ơn quý khách đã sử dụng dịch vụ."),
            _ => (null, null)
        };

        if (title == null) return;

        var link = string.Equals(next, "CLOSED", StringComparison.OrdinalIgnoreCase)
            ? $"/my-vehicles/{order.VehicleId}?tab=history&serviceOrderId={order.Id}&notificationType=service_status"
            : $"/my-garage-dashboard?serviceOrderId={order.Id}&notificationType=service_status";

        _context.Notifications.Add(new Notification
        {
            CustomerId = order.CustomerId,
            Title = title,
            Content = content!,
            IsRead = false,
            SentAt = _clock.UtcNow,
            LinkAction = link
        });
    }
}
