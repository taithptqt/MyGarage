using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ICommissionTransactionService : IGenericService<CommissionTransaction>
    {
        List<CommissionTransactionResponseDto> MapToDtos(IEnumerable<CommissionTransaction> items);
        CommissionTransactionResponseDto MapToDto(CommissionTransaction item);
        Task<CommissionTransactionResponseDto> CreateDtoAsync(CommissionTransactionCreateDto itemDto);
        void MapUpdate(CommissionTransactionUpdateDto itemDto, CommissionTransaction existingItem);
    }
}
