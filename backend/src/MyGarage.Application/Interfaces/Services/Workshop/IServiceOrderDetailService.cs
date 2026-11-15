using System.Collections.Generic;
using System.Threading;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IServiceOrderDetailService : IGenericService<ServiceOrderDetail>
    {
        IEnumerable<ServiceOrderDetailResponseDto> MapToDtos(IEnumerable<ServiceOrderDetail> items);
        ServiceOrderDetailResponseDto MapToDto(ServiceOrderDetail item);
        ServiceOrderDetail MapToEntity(ServiceOrderDetailCreateDto itemDto);
        void ApplyUpdate(ServiceOrderDetailUpdateDto itemDto, ServiceOrderDetail existingItem);
        Task<ServiceOrderDetailResponseDto?> GetDtoByIdAsync(long id);
        Task<IReadOnlyList<ServiceOrderDetail>> GetByServiceOrderIdAsync(long serviceOrderId, CancellationToken ct = default);
        Task SyncServiceOrderWaitingPartsStatusAsync(long serviceOrderId, long? actorStaffId = null);
    }
}
