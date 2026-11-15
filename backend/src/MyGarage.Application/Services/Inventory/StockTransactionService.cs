using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class StockTransactionService : GenericService<StockTransaction>, IStockTransactionService
    {
        private readonly IMapper _mapper;

        public StockTransactionService(IStockTransactionRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<StockTransactionResponseDto> MapToDtos(IEnumerable<StockTransaction> items)
        {
            return _mapper.Map<List<StockTransactionResponseDto>>(items);
        }

        public async Task<StockTransactionResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<StockTransactionResponseDto>(item);
        }

        public async Task<StockTransactionResponseDto> CreateDtoAsync(StockTransactionCreateDto itemDto)
        {
            var entity = _mapper.Map<StockTransaction>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<StockTransactionResponseDto>(createdItem);
        }

        public async Task<StockTransactionResponseDto?> UpdateDtoAsync(long id, StockTransactionUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<StockTransactionResponseDto>(updatedItem);
        }
    }
}
