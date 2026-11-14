using MyGarage.Models;

namespace MyGarage.Interfaces;

public interface IServiceOrderCustomerLifecycleNotifier
{
    /// <summary>Queue a customer notification when lifecycle status changes (same DbContext SaveChanges).</summary>
    void EnqueueAfterStatusChange(ServiceOrder order, string previousStatus);
}
