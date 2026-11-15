using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class BayService : GenericService<Bay>, IBayService
    {
        private readonly IMapper _mapper;

        public BayService(IBayRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<BayResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<BayResponseDto>>(items);
        }

        public async Task<BayResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<BayResponseDto>(item);
        }

        public async Task<BayResponseDto> CreateDtoAsync(BayCreateDto itemDto)
        {
            var entity = _mapper.Map<Bay>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<BayResponseDto>(createdItem);
        }

        public async Task<BayResponseDto?> UpdateDtoAsync(long id, BayUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<BayResponseDto>(updatedItem);
        }

        public override async Task<Bay> AddAsync(Bay entity)
        {
            NormalizeServiceArea(entity);
            return await base.AddAsync(entity);
        }

        public override async Task<Bay?> UpdateAsync(object id, Bay entity)
        {
            NormalizeServiceArea(entity);
            return await base.UpdateAsync(id, entity);
        }

        private static void NormalizeServiceArea(Bay entity)
        {
            entity.ServiceArea = (entity.ServiceArea ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
