using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class MaterialRequestDetailService : GenericService<MaterialRequestDetail>, IMaterialRequestDetailService
    {
        private readonly IMapper _mapper;

        public MaterialRequestDetailService(IMaterialRequestDetailRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<MaterialRequestDetailResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<MaterialRequestDetailResponseDto>>(items);
        }

        public List<MaterialRequestDetailResponseDto> MapToDtos(IEnumerable<MaterialRequestDetail> items)
        {
            return _mapper.Map<List<MaterialRequestDetailResponseDto>>(items);
        }

        public async Task<MaterialRequestDetailResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<MaterialRequestDetailResponseDto>(item);
        }

        public async Task<MaterialRequestDetailResponseDto> CreateDtoAsync(MaterialRequestDetailCreateDto itemDto)
        {
            var entity = _mapper.Map<MaterialRequestDetail>(itemDto);
            entity.ReservedQuantity = 0m;
            var createdItem = await AddAsync(entity);
            return _mapper.Map<MaterialRequestDetailResponseDto>(createdItem);
        }

        public async Task<MaterialRequestDetailResponseDto?> UpdateDtoAsync(long id, MaterialRequestDetailUpdateDto itemDto, MaterialRequestDetail? existingItem = null)
        {
            existingItem ??= await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<MaterialRequestDetailResponseDto>(updatedItem);
        }
    }
}
