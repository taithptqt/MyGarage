using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderHandoverQueryStore
{
    Task<ServiceOrder?> GetServiceOrderWithDetailsAsNoTrackingAsync(long id);
    Task<ServiceOrder?> GetServiceOrderForUpdateAsync(long id);
}
