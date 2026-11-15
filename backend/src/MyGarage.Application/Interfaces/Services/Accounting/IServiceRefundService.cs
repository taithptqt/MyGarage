using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IServiceRefundService : IGenericService<ServiceRefund>
    {
        Task<IEnumerable<ServiceRefundResponseDto>> GetAllDtosAsync();
        Task<ServiceRefundResponseDto?> GetDtoByIdAsync(long id);
        Task<ServiceRefundResponseDto> CreateDtoAsync(ServiceRefundCreateDto itemDto, long actorUserId);
        Task<ServiceRefundResponseDto?> UpdateDtoAsync(long id, ServiceRefundUpdateDto itemDto, long actorUserId);
    }
}
