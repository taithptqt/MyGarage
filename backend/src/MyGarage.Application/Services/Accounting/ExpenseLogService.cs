using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ExpenseLogService : GenericService<ExpenseLog>, IExpenseLogService
    {
        private readonly IMapper _mapper;

        public ExpenseLogService(IExpenseLogRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<ExpenseLogResponseDto> MapToDtos(IEnumerable<ExpenseLog> items)
            => _mapper.Map<List<ExpenseLogResponseDto>>(items);

        public async Task<ExpenseLogResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ExpenseLogResponseDto>(item);
        }

        public async Task<ExpenseLogResponseDto> CreateDtoAsync(ExpenseLogCreateDto itemDto)
        {
            var entity = _mapper.Map<ExpenseLog>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ExpenseLogResponseDto>(createdItem);
        }

        public async Task<ExpenseLogResponseDto?> UpdateDtoAsync(long id, ExpenseLogUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ExpenseLogResponseDto>(updatedItem);
        }
    }
}
