using System.Threading.Tasks;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses.Execution;

namespace MyGarage.Interfaces
{
    public interface IConsumableReturnService
    {
        Task<ConsumableReturnBundleDto> CreateBundleAsync(ConsumableReturnCreateRequestDto request, long actorStaffId);
    }
}
