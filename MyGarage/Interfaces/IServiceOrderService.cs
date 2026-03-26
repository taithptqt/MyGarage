using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IServiceOrderService : IGenericService<ServiceOrder>
    {
        Task<bool> HasCheckInLogAsync(long serviceOrderId);
        Task<bool> HasCheckOutLogAsync(long serviceOrderId);
    }
}
