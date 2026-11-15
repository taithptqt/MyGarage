using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderBillingQueryStore
{
    Task<List<ServiceOrderDetail>> GetEditableServiceDetailsAsync(long serviceOrderId);
}
