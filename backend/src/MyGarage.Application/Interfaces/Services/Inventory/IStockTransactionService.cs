using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IStockTransactionService : IGenericService<StockTransaction>
    {
        List<StockTransactionResponseDto> MapToDtos(IEnumerable<StockTransaction> items);
        Task<StockTransactionResponseDto?> GetDtoByIdAsync(long id);
        Task<StockTransactionResponseDto> CreateDtoAsync(StockTransactionCreateDto itemDto);
        Task<StockTransactionResponseDto?> UpdateDtoAsync(long id, StockTransactionUpdateDto itemDto);
    }
}
