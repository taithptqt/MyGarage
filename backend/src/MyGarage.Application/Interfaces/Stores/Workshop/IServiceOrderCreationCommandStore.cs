using System;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderCreationCommandStore
{
    Task<ServiceOrder> CreateWithUniqueCodeAsync(
        ServiceOrder entity,
        Func<string> generateCode,
        Func<ServiceOrder, Task> persistAndAfterCreateAsync);
}
