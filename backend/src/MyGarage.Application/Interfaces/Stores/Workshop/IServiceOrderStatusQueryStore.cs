using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderStatusQueryStore
{
    Task<string?> GetServiceOrderStatusAsync(long serviceOrderId);
}
