using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class BranchExceptionService : GenericService<BranchException>, IBranchExceptionService
    {
        private readonly IMapper _mapper;

        public BranchExceptionService(IBranchExceptionRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<BranchExceptionResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<BranchExceptionResponseDto>>(items);
        }

        public async Task<BranchExceptionResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<BranchExceptionResponseDto>(item);
        }

        public async Task<BranchExceptionResponseDto> CreateDtoAsync(BranchExceptionCreateDto itemDto)
        {
            var entity = _mapper.Map<BranchException>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<BranchExceptionResponseDto>(createdItem);
        }

        public async Task<BranchExceptionResponseDto?> UpdateDtoAsync(long id, BranchExceptionUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<BranchExceptionResponseDto>(updatedItem);
        }
    }
}
