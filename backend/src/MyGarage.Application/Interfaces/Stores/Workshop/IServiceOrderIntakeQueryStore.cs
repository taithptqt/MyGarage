using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderIntakeQueryStore
{
    Task<bool> HasCheckInConditionLogAsync(long serviceOrderId, string logType);
}
