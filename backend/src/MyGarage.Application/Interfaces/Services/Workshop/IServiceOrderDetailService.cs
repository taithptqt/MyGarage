using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IServiceOrderDetailService : IGenericService<ServiceOrderDetail>
    {
        Task SyncServiceOrderWaitingPartsStatusAsync(long serviceOrderId, long? actorStaffId = null);
    }
}
