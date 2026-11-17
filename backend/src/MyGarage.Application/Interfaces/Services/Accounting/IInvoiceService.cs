using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public enum InvoiceMutationStatus
    {
        Ok,
        Created,
        NotFound,
        Forbidden,
        BadRequest
    }

    public sealed record InvoiceMutationResult(
        InvoiceMutationStatus Status,
        InvoiceResponseDto? Data = null,
        string? ErrorMessage = null);

    public interface IInvoiceService : IGenericService<Invoice>
    {
        List<InvoiceResponseDto> MapToDtos(IEnumerable<Invoice> items);
        InvoiceResponseDto MapToDto(Invoice item);
        Task<InvoiceResponseDto?> GetDtoByIdAsync(long id);
        Task<(IReadOnlyList<InvoiceResponseDto> Items, int Total)> GetScopedPageDtosAsync(
            long? branchId,
            int page,
            int pageSize,
            CancellationToken ct = default);
        Task<InvoiceResponseDto?> GetScopedDtoByIdAsync(
            long id,
            long? branchId,
            CancellationToken ct = default);
        Task<InvoiceMutationResult> CreateOrUpdateForContextAsync(
            InvoiceCreateDto? itemDto,
            long actorUserId,
            long? scopedBranchId,
            UserActorContext actor,
            CancellationToken ct = default);
        Task<Invoice?> EnsureForPaymentContextAsync(long? serviceOrderId, long? retailOrderId, string? payerType, long? payerId);
        Task<(Invoice? Invoice, bool Created)> EnsureForPaymentContextWithOutcomeAsync(long? serviceOrderId, long? retailOrderId, string? payerType, long? payerId);
        Task<InvoiceBackfillResponseDto> BackfillCompletedPaymentsAsync();
    }
}
