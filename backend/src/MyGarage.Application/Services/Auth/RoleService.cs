using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class RoleService : GenericService<Role>, IRoleService
    {
        private readonly IMapper _mapper;

        public RoleService(IRoleRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<RoleResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<RoleResponseDto>>(items);
        }

        public async Task<RoleResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<RoleResponseDto>(item);
        }

        public async Task<RoleResponseDto> CreateDtoAsync(RoleCreateDto itemDto)
        {
            var entity = _mapper.Map<Role>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<RoleResponseDto>(createdItem);
        }

        public async Task<RoleResponseDto?> UpdateDtoAsync(long id, RoleUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<RoleResponseDto>(updatedItem);
        }
    }
}
