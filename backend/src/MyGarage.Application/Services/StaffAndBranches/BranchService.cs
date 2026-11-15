using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class BranchService : GenericService<Branch>, IBranchService
    {
        private readonly IMapper _mapper;

        public BranchService(IBranchRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<BranchResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<BranchResponseDto>>(items);
        }

        public List<BranchResponseDto> MapToDtos(IEnumerable<Branch> items)
        {
            return _mapper.Map<List<BranchResponseDto>>(items);
        }

        public async Task<BranchResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<BranchResponseDto>(item);
        }

        public async Task<BranchResponseDto> CreateDtoAsync(BranchCreateDto itemDto)
        {
            var entity = _mapper.Map<Branch>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<BranchResponseDto>(createdItem);
        }

        public async Task<BranchResponseDto?> UpdateDtoAsync(long id, BranchUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<BranchResponseDto>(updatedItem);
        }
    }
}
