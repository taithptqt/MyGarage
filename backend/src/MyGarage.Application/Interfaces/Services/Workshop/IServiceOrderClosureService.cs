using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    /// <summary>
    /// Handles post-closure side effects for service orders, including service advisor commission
    /// generation and maintenance reminder scheduling.
    /// </summary>
    public interface IServiceOrderClosureService
    {
        Task HandleOrderClosureAsync(
            ServiceOrder order,
            string? previousStatus,
            long? actorUserId);
    }
}
