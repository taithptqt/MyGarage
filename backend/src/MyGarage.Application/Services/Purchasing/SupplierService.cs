using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class SupplierService : GenericService<Supplier>, ISupplierService
    {
        private readonly IMapper _mapper;

        public SupplierService(ISupplierRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<SupplierResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<SupplierResponseDto>>(items);
        }

        public List<SupplierResponseDto> MapToDtos(IEnumerable<Supplier> items)
        {
            return _mapper.Map<List<SupplierResponseDto>>(items);
        }

        public async Task<SupplierResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<SupplierResponseDto>(item);
        }

        public async Task<SupplierResponseDto> CreateDtoAsync(SupplierCreateDto itemDto)
        {
            var entity = _mapper.Map<Supplier>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<SupplierResponseDto>(createdItem);
        }

        public async Task<SupplierResponseDto?> UpdateDtoAsync(long id, SupplierUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<SupplierResponseDto>(updatedItem);
        }
    }
}
