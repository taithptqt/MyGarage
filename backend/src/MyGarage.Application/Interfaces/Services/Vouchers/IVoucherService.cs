using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVoucherService : IGenericService<Voucher>
    {
        Task<(IReadOnlyList<VoucherResponseDto> Items, int Total)> GetPageDtosByIdAsync(int page, int pageSize);
        Task<VoucherResponseDto?> GetDtoByIdAsync(long id);
        Task<VoucherResponseDto> CreateDtoAsync(VoucherCreateDto itemDto);
        Task<VoucherResponseDto?> UpdateDtoAsync(long id, VoucherUpdateDto itemDto);
        Task<VoucherResponseDto?> UpdateStatusDtoAsync(long id, string status, bool isActive);
    }
}
