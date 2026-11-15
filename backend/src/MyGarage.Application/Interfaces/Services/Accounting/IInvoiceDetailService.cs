using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IInvoiceDetailService : IGenericService<InvoiceDetail>
    {
        Task<IEnumerable<InvoiceDetailResponseDto>> GetAllDtosAsync();
        IEnumerable<InvoiceDetailResponseDto> MapToDtos(IEnumerable<InvoiceDetail> items);
        InvoiceDetailResponseDto MapToDto(InvoiceDetail item);
        Task<InvoiceDetailResponseDto?> GetDtoByIdAsync(long id);
        Task<InvoiceDetailResponseDto> CreateDtoAsync(InvoiceDetailCreateDto itemDto);
        Task<InvoiceDetailResponseDto?> UpdateDtoAsync(long id, InvoiceDetailUpdateDto itemDto);
        Task<IEnumerable<InvoiceDetailResponseDto>> GetScopedDtosAsync(long? branchId, CancellationToken ct = default);
        Task<InvoiceDetailResponseDto?> GetScopedDtoByIdAsync(long id, long? branchId, CancellationToken ct = default);
        Task<InvoiceDetailCommandResult<InvoiceDetailResponseDto>> CreateScopedAsync(long? branchId, InvoiceDetailCreateDto? itemDto, CancellationToken ct = default);
        Task<InvoiceDetailCommandResult<InvoiceDetailResponseDto>> UpdateScopedAsync(long id, long? branchId, InvoiceDetailUpdateDto? itemDto, CancellationToken ct = default);
        Task<InvoiceDetailCommandResult<bool>> DeleteScopedAsync(long id, long? branchId, CancellationToken ct = default);
        Task<string?> ValidateInvoiceDetailMutationAsync(long? invoiceId, long? fallbackInvoiceId = null, CancellationToken ct = default);
    }
}
