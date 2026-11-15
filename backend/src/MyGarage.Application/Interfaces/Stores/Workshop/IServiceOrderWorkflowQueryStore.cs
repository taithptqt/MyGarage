using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderWorkflowQueryStore
{
    Task<ServiceOrder?> GetServiceOrderWithDiscountsAsync(long id);
}
